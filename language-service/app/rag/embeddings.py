"""Embedding providers for the grammar retriever.

Voyage AI is used when VOYAGE_API_KEY is set (strong on German, negligible cost).
Without a key we fall back to a deterministic hashing embedder so retrieval still
works offline — in CI, in local dev, and if the Voyage call fails. Both providers
emit unit-length vectors of the same width, so the storage and search path is
identical either way; the active model name is recorded per chunk so a provider
switch triggers a re-index instead of silently mixing vector spaces.
"""
import hashlib
import logging
import math
import os
import re

logger = logging.getLogger("language-service")

DIM = 512
VOYAGE_MODEL = os.getenv("VOYAGE_MODEL", "voyage-3-lite")
VOYAGE_URL = "https://api.voyageai.com/v1/embeddings"
# Voyage limits how many texts one request may carry; the corpus is embedded in batches.
VOYAGE_BATCH = int(os.getenv("VOYAGE_BATCH", "100"))

_TOKEN_RE = re.compile(r"[\wäöüßÄÖÜ]+", re.UNICODE)


def voyage_enabled() -> bool:
    return bool(os.getenv("VOYAGE_API_KEY"))


def model_name() -> str:
    """Identifies the vector space currently in use, stored alongside each chunk."""
    return VOYAGE_MODEL if voyage_enabled() else f"hashing-{DIM}"


# ---------------- deterministic fallback ----------------

def _tokens(text: str) -> list[str]:
    """Word tokens plus character 4-grams — the n-grams keep German compounds and
    inflected forms ("Wechselpräposition" vs "Wechselpräpositionen") close together."""
    words = [w.lower() for w in _TOKEN_RE.findall(text)]
    grams: list[str] = []
    for w in words:
        grams.append(w)
        if len(w) > 4:
            grams += [f"#{w[i:i + 4]}" for i in range(len(w) - 3)]
    return grams


def _bucket(token: str) -> int:
    return int.from_bytes(hashlib.blake2b(token.encode("utf-8"), digest_size=4).digest(), "big") % DIM


# Inverse document frequency over the indexed corpus. Without it, ubiquitous words ("steht",
# "der") weigh as much as the words that actually identify a rule ("Nebensatz",
# "Wechselpräposition"), and short exercise snippets outrank the lesson that explains the topic.
_IDF: list[float] | None = None


def fit_idf(texts: list[str]) -> list[float]:
    df = [0] * DIM
    for text in texts:
        for b in {_bucket(tok) for tok in _tokens(text)}:
            df[b] += 1
    n = len(texts) or 1
    return [math.log((n + 1) / (d + 1)) + 1.0 for d in df]


def set_idf(idf: list[float] | None) -> None:
    global _IDF
    _IDF = idf


def get_idf() -> list[float] | None:
    return _IDF


def _hash_embed(text: str) -> list[float]:
    """Sublinear term frequency, weighted by corpus IDF when available, L2-normalised."""
    counts: dict[int, float] = {}
    for tok in _tokens(text):
        b = _bucket(tok)
        counts[b] = counts.get(b, 0.0) + 1.0
    vec = [0.0] * DIM
    for b, c in counts.items():
        vec[b] = (1.0 + math.log(c)) * (_IDF[b] if _IDF else 1.0)
    norm = math.sqrt(sum(v * v for v in vec))
    return [v / norm for v in vec] if norm else vec


# ---------------- Voyage ----------------

def _voyage_embed(texts: list[str], input_type: str) -> list[list[float]] | None:
    try:
        import httpx

        resp = httpx.post(
            VOYAGE_URL,
            headers={"Authorization": f"Bearer {os.environ['VOYAGE_API_KEY']}"},
            json={"model": VOYAGE_MODEL, "input": texts, "input_type": input_type},
            timeout=30.0,
        )
        resp.raise_for_status()
        rows = sorted(resp.json()["data"], key=lambda d: d["index"])
        return [r["embedding"] for r in rows]
    except Exception as exc:  # noqa: BLE001 — any failure falls back to hashing
        logger.warning("Voyage embedding failed, using offline hashing embedder: %s", exc)
        return None


# ---------------- public API ----------------

def embed_documents(texts: list[str]) -> list[list[float]]:
    """Embeds the whole corpus. Voyage caps how many texts one request may carry, so the
    corpus is sent in batches; if any batch fails the *entire* corpus falls back to the
    hashing embedder, because mixing two vector spaces in one index makes every similarity
    score meaningless."""
    if not texts:
        return []
    if voyage_enabled():
        out: list[list[float]] = []
        for i in range(0, len(texts), VOYAGE_BATCH):
            vecs = _voyage_embed(texts[i:i + VOYAGE_BATCH], "document")
            if not vecs:
                out = []
                break
            out.extend(vecs)
        if len(out) == len(texts):
            return out
        logger.warning("Voyage embedding incomplete — indexing the whole corpus with the "
                       "offline embedder instead so the vector space stays consistent.")
    return [_hash_embed(t) for t in texts]


def embed_query(text: str) -> list[float]:
    if voyage_enabled():
        vecs = _voyage_embed([text], "query")
        if vecs:
            return vecs[0]
    return _hash_embed(text)


def cosine(a: list[float], b: list[float]) -> float:
    """Both providers return unit vectors, but normalise defensively."""
    dot = sum(x * y for x, y in zip(a, b))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    return dot / (na * nb) if na and nb else 0.0
