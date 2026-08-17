"""Retrieval-quality gate for the curriculum RAG.

Runs the golden set through the pipeline exactly as the code currently ships and fails the
build if quality drops below the bar: recall@5 must be >= 0.8 and no query may return zero
results. Offline (hashing embedder, in-memory store) so it runs keyless in CI.
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import harness  # noqa: E402


def test_no_query_returns_zero_results():
    m = harness.evaluate(harness.current_config())
    assert m["zero_results_rate"] == 0.0, f"queries returned nothing: {m['misses']}"


def test_recall_at_5_meets_bar():
    m = harness.evaluate(harness.current_config())
    assert m["recall@5"] >= 0.8, f"recall@5={m['recall@5']} < 0.8; misses={m['misses']}"
