"""Indexing and retrieval over the app's own grammar content.

The corpus is pushed in by the .NET API (which owns the curriculum database): lesson
explanations and exercise rationales, each tagged with its CEFR level and grammar topic.
Retrieval is always free; the optional grounded answer is the only part that spends tokens,
and the caller (the .NET API) gates that behind the existing AI quota.
"""
import hashlib
import logging
import os
import re

from .. import claude_client, prompts
from . import embeddings, store

logger = logging.getLogger("language-service")

MAX_CHARS = 700
MIN_CHARS = 40
# Similarity below this is treated as "no match" rather than a confident wrong answer.
MIN_SCORE = float(os.getenv("RAG_MIN_SCORE", "0.20"))
# Ranking preferences, small enough that a clearly better match still wins on relevance.
LEVEL_BOOST = float(os.getenv("RAG_LEVEL_BOOST", "0.05"))
SOURCE_BOOST = float(os.getenv("RAG_SOURCE_BOOST", "0.05"))


def _chunk_id(source: str, title: str, ordinal: int, text: str) -> str:
    digest = hashlib.blake2b(f"{source}|{title}|{ordinal}|{text}".encode("utf-8"), digest_size=8)
    return digest.hexdigest()


def _split(text: str) -> list[str]:
    """Split on blank lines, then pack paragraphs up to MAX_CHARS so a chunk keeps
    a whole explanation together rather than cutting mid-rule."""
    paras = [p.strip() for p in re.split(r"\n\s*\n", text or "") if p.strip()]
    chunks: list[str] = []
    buf = ""
    for p in paras:
        if len(p) > MAX_CHARS:
            if buf:
                chunks.append(buf)
                buf = ""
            for i in range(0, len(p), MAX_CHARS):
                chunks.append(p[i:i + MAX_CHARS])
            continue
        if len(buf) + len(p) + 2 <= MAX_CHARS:
            buf = f"{buf}\n\n{p}" if buf else p
        else:
            chunks.append(buf)
            buf = p
    if buf:
        chunks.append(buf)
    return [c for c in chunks if len(c) >= MIN_CHARS]


def build_chunks(docs: list[dict]) -> list[store.Chunk]:
    """docs: [{level, source, title, grammarTopic, text}] as pushed by the .NET API."""
    out: list[store.Chunk] = []
    for d in docs:
        level = (d.get("level") or "").upper()
        source = d.get("source") or "lesson"
        title = d.get("title") or ""
        topic = d.get("grammarTopic") or None
        for i, piece in enumerate(_split(d.get("text") or "")):
            # Prefix the heading so the topic itself is part of what gets embedded.
            body = f"{title}" + (f" ({topic})" if topic else "") + f"\n{piece}"
            out.append(store.Chunk(
                id=_chunk_id(source, title, i, piece),
                level=level, source=source, title=title,
                grammar_topic=topic, text=body,
            ))
    # De-duplicate identical chunks (the same rule can be repeated across lessons).
    seen: set[str] = set()
    unique: list[store.Chunk] = []
    for c in out:
        if c.id in seen:
            continue
        seen.add(c.id)
        unique.append(c)
    return unique


def index(docs: list[dict]) -> dict:
    chunks = build_chunks(docs)
    if not chunks:
        return {"indexed": 0, **store.get_store().stats()}

    # Fit IDF over the corpus *before* embedding, so documents and later queries share weights.
    idf = None
    if not embeddings.voyage_enabled():
        idf = embeddings.fit_idf([c.text for c in chunks])
    embeddings.set_idf(idf)

    vectors = embeddings.embed_documents([c.text for c in chunks])
    for c, v in zip(chunks, vectors):
        c.embedding = v
    st = store.get_store()
    n = st.replace_all(chunks, embeddings.model_name(), idf)
    logger.info("rag: indexed %s chunks with %s", n, embeddings.model_name())
    return {"indexed": n, **st.stats()}


def _ensure_idf(st) -> None:
    """After a restart the process has no IDF in memory; reload what the index was built with."""
    if embeddings.voyage_enabled() or embeddings.get_idf() is not None:
        return
    loader = getattr(st, "load_idf", None)
    if loader:
        embeddings.set_idf(loader())


def search(query: str, level: str | None = None, k: int = 4) -> list[store.Hit]:
    """Retrieve across the whole corpus, preferring — but never restricting to — the
    learner's level.

    Restricting was wrong: the rule someone needs is frequently taught at a different level
    than the exercise they are stuck on (Nebensätze is a B1 lesson, Wechselpräpositionen an
    A2 one), and a hard filter silently returned unrelated same-level material instead. The
    level is applied as a ranking preference, and each hit reports the level it came from.
    """
    if not (query or "").strip():
        return []
    st = store.get_store()
    _ensure_idf(st)
    qv = embeddings.embed_query(query)

    pool = st.search(qv, None, max(k * 5, 20))
    want = (level or "").upper()

    def rank(hit: store.Hit) -> float:
        bonus = LEVEL_BOOST if want and hit.chunk.level == want else 0.0
        # Lessons explain the rule; exercise rationales illustrate it. Prefer the explanation.
        bonus += SOURCE_BOOST if hit.chunk.source == "lesson" else 0.0
        return hit.score + bonus

    pool.sort(key=rank, reverse=True)
    # Better to admit we found nothing than to present an unrelated rule as the answer.
    return [h for h in pool if h.score >= MIN_SCORE][:k]


def _sources(hits: list[store.Hit]) -> list[dict]:
    return [
        {
            "title": h.chunk.title,
            "grammarTopic": h.chunk.grammar_topic,
            "level": h.chunk.level,
            "source": h.chunk.source,
            "text": h.chunk.text,
            "score": round(h.score, 4),
        }
        for h in hits
    ]


def answer(query: str, level: str | None = None, k: int = 4, with_answer: bool = False) -> dict:
    """Retrieval always runs; the Claude answer is generated only when asked for and
    is grounded strictly in the retrieved chunks."""
    hits = search(query, level, k)
    result = {
        "query": query,
        "sources": _sources(hits),
        "answer": None,
        "grounded": False,
        "retrieval": store.get_store().stats().get("store", "memory"),
    }
    if not with_answer or not hits:
        return result

    context = "\n\n---\n\n".join(
        f"[{i + 1}] {h.chunk.title}" + (f" — {h.chunk.grammar_topic}" if h.chunk.grammar_topic else "")
        + f"\n{h.chunk.text}"
        for i, h in enumerate(hits)
    )
    data = claude_client.call_json(
        prompts.RAG_SYSTEM.format(level=level or "B1"),
        prompts.RAG_USER.format(question=query, context=context),
    )
    if data and data.get("answer"):
        result["answer"] = data["answer"]
        result["grounded"] = bool(data.get("grounded", True))
    else:
        # No key or the call failed: fall back to the best retrieved explanation verbatim.
        result["answer"] = hits[0].chunk.text
        result["grounded"] = True
    return result
