"""CI wiring for the grader eval suite.

Contract checks are absolute — a malformed grader response breaks the .NET DTO binding,
so they must hold with or without an API key. Quality metrics are only checked against the
committed baseline for the same mode, because the offline fallback is legitimately much
weaker than Claude and we don't want CI to pretend otherwise.
"""
import os

import pytest

from evals import metrics, run as evals_run

RESULT = evals_run.run()


def test_writing_contract_valid_for_every_case():
    failures = [(r["id"], r["violations"]) for r in RESULT["writing"] if not r["contract_ok"]]
    assert not failures, f"malformed writing responses: {failures}"


def test_speaking_contract_valid_for_every_case():
    failures = [(r["id"], r["violations"]) for r in RESULT["speaking"] if not r["contract_ok"]]
    assert not failures, f"malformed speaking responses: {failures}"


def test_no_regression_against_baseline():
    assert evals_run.check(RESULT) == 0, "grader metrics regressed past the baseline"


# ---------------- dataset integrity ----------------

def test_dataset_cases_are_wellformed():
    for case in evals_run.load("writing.jsonl"):
        exp = case["expected"]
        lo, hi = exp["score_band"]
        assert 0 <= lo <= hi <= 100, f"{case['id']}: bad score band"
        assert exp["cefr"] in metrics.LEVELS, f"{case['id']}: bad expected CEFR"
        assert case["level"] in metrics.LEVELS, f"{case['id']}: bad level"


def test_dataset_exercises_cefr_estimation():
    """Guards a real trap: if every case's expected CEFR equals the requested level, a grader
    that merely echoes the input scores 100% on cefr_exact and the metric measures nothing."""
    cases = evals_run.load("writing.jsonl")
    mismatched = [c for c in cases if c["expected"]["cefr"] != c["level"]]
    assert len(mismatched) >= 2, (
        "dataset needs cases where the text's real level differs from the requested level, "
        "otherwise cefr_exact is meaningless"
    )


def test_dataset_covers_clean_and_faulty_texts():
    cases = evals_run.load("writing.jsonl")
    clean = [c for c in cases if not c["expected"]["categories"] and not c["expected"]["must_flag"]]
    faulty = [c for c in cases if c["expected"]["must_flag"]]
    assert clean and faulty, "need both clean and error-bearing texts to measure false alarms"


def test_clean_cases_meet_their_length_requirement():
    """A 'clean' text that is under min_words would make a correct task-flag look like a
    false alarm, quietly inflating false_alarm_rate."""
    import re
    for case in evals_run.load("writing.jsonl"):
        exp = case["expected"]
        if exp["categories"] or exp["must_flag"] or not case["text"]:
            continue
        words = len(re.findall(r"\b\w+\b", case["text"]))
        assert words >= case["min_words"], f"{case['id']}: clean case is under min_words"


# ---------------- live model ----------------

live = pytest.mark.skipif(
    not os.getenv("ANTHROPIC_API_KEY"), reason="live grader eval needs ANTHROPIC_API_KEY"
)


@live
def test_live_grader_beats_the_offline_fallback_on_error_detection():
    """The whole point of paying for Claude: it should actually catch planted errors."""
    assert RESULT["mode"] == "live"
    assert RESULT["summary"]["writing"]["error_flag_recall"] > 0.5


@live
def test_live_grader_estimates_cefr_rather_than_echoing_the_level():
    assert RESULT["summary"]["writing"]["cefr_adjacent"] >= 0.85
