"""Offline retrieval-eval harness for the curriculum RAG.

Indexes the extracted corpus with a given knob config using the in-memory store and the
deterministic hashing embedder — no DB, no network — then scores the golden queries with
recall@1, recall@5, MRR and a zero-results rate. Used by the pytest gate and the tuning sweep.
"""
from __future__ import annotations

import json
import pathlib

import yaml

from app.rag import embeddings, retriever, store

HERE = pathlib.Path(__file__).parent
CORPUS = json.loads((HERE / "corpus.json").read_text(encoding="utf-8"))
QUERIES = yaml.safe_load((HERE / "golden_queries.yaml").read_text(encoding="utf-8"))
K = 5

# The knobs the pipeline ships with (mirrors the module constants; IDF + n-grams are on).
DEFAULT_CONFIG = {
    "max_chars": 700, "min_score": 0.20, "level_boost": 0.05, "source_boost": 0.05,
    "dim": 512, "use_idf": True, "use_ngrams": True,
}


def current_config() -> dict:
    """The config that reflects the code as it stands right now (so the gate tests what ships)."""
    return {
        "max_chars": retriever.MAX_CHARS, "min_score": retriever.MIN_SCORE,
        "level_boost": retriever.LEVEL_BOOST, "source_boost": retriever.SOURCE_BOOST,
        "dim": embeddings.DIM, "use_idf": True, "use_ngrams": True,
    }


def _apply(config: dict):
    """Set the knobs on the modules; returns a restore() that puts everything back."""
    orig = {
        "MAX_CHARS": retriever.MAX_CHARS, "MIN_SCORE": retriever.MIN_SCORE,
        "LEVEL_BOOST": retriever.LEVEL_BOOST, "SOURCE_BOOST": retriever.SOURCE_BOOST,
        "DIM": embeddings.DIM, "fit_idf": embeddings.fit_idf, "_tokens": embeddings._tokens,
    }
    retriever.MAX_CHARS = config["max_chars"]
    retriever.MIN_SCORE = config["min_score"]
    retriever.LEVEL_BOOST = config["level_boost"]
    retriever.SOURCE_BOOST = config["source_boost"]
    embeddings.DIM = config["dim"]
    if not config["use_idf"]:
        embeddings.fit_idf = lambda texts: None            # neutral weighting
    if not config["use_ngrams"]:
        _re = embeddings._TOKEN_RE
        embeddings._tokens = lambda text: [w.lower() for w in _re.findall(text)]

    def restore():
        retriever.MAX_CHARS = orig["MAX_CHARS"]
        retriever.MIN_SCORE = orig["MIN_SCORE"]
        retriever.LEVEL_BOOST = orig["LEVEL_BOOST"]
        retriever.SOURCE_BOOST = orig["SOURCE_BOOST"]
        embeddings.DIM = orig["DIM"]
        embeddings.fit_idf = orig["fit_idf"]
        embeddings._tokens = orig["_tokens"]

    return restore


def _hit_rank(hits, expect) -> int:
    """1-based rank of the first hit whose title or grammar topic is expected; 0 if none."""
    exp = set(expect)
    for i, h in enumerate(hits, start=1):
        if h.chunk.title in exp or h.chunk.grammar_topic in exp:
            return i
    return 0


def evaluate(config: dict) -> dict:
    restore = _apply(config)
    try:
        store.reset_store_for_tests()
        retriever.index(CORPUS)
        rows = []
        for q in QUERIES:
            hits = retriever.search(q["query"], q.get("level"), k=K)
            rows.append({"id": q["id"], "hard": bool(q.get("hard")),
                         "rank": _hit_rank(hits, q["expect"]), "n": len(hits)})
    finally:
        restore()
        store.reset_store_for_tests()

    n = len(rows)
    hit5 = [r for r in rows if 1 <= r["rank"] <= K]
    hard = [r for r in rows if r["hard"]]
    return {
        "recall@1": round(sum(1 for r in rows if r["rank"] == 1) / n, 4),
        "recall@5": round(len(hit5) / n, 4),
        "mrr": round(sum((1.0 / r["rank"]) if r["rank"] else 0.0 for r in rows) / n, 4),
        "zero_results_rate": round(sum(1 for r in rows if r["n"] == 0) / n, 4),
        "hard_recall@5": round(sum(1 for r in hard if 1 <= r["rank"] <= K) / len(hard), 4) if hard else None,
        "misses": [r["id"] for r in rows if not (1 <= r["rank"] <= K)],
        "rows": rows,
    }


if __name__ == "__main__":
    import sys
    m = evaluate(DEFAULT_CONFIG)
    print(json.dumps({k: v for k, v in m.items() if k != "rows"}, ensure_ascii=False, indent=2))
    sys.exit(0)
