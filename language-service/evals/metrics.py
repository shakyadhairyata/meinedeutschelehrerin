"""Metrics for the writing/speaking grader.

Two families, deliberately separated:

* **Contract** — did the grader return a well-formed, bindable response at all? This must
  hold for every case in every mode, keys or no keys, because the .NET DTOs depend on it.
* **Quality** — how close is the judgement to a human label (score band, CEFR level, which
  errors were caught)? The deterministic offline fallback is expected to score poorly here;
  that gap is precisely what the suite is meant to quantify.
"""
import re

LEVELS = ["A1", "A2", "B1", "B2", "C1"]
CORRECTION_KEYS = {"original", "correction", "explanation", "category"}
_WORD_RE = re.compile(r"[\wäöüßÄÖÜ]+", re.UNICODE)


def _norm(s: str) -> str:
    return " ".join(_WORD_RE.findall((s or "").lower()))


def _tokens(s: str) -> list[str]:
    return _norm(s).split()


# ---------------- contract ----------------

def writing_violations(res: dict) -> list[str]:
    bad: list[str] = []
    if not isinstance(res, dict):
        return ["response is not an object"]
    score = res.get("scorePercent")
    if not isinstance(score, (int, float)):
        bad.append("scorePercent missing or not a number")
    elif not 0 <= float(score) <= 100:
        bad.append(f"scorePercent out of range: {score}")
    if not isinstance(res.get("summary"), str):
        bad.append("summary missing or not a string")
    if not isinstance(res.get("strengths"), list):
        bad.append("strengths is not a list")
    if not isinstance(res.get("correctedText"), str):
        bad.append("correctedText missing or not a string")
    if res.get("cefrEstimate") not in LEVELS:
        bad.append(f"cefrEstimate not a CEFR level: {res.get('cefrEstimate')!r}")
    corrections = res.get("corrections")
    if not isinstance(corrections, list):
        bad.append("corrections is not a list")
    else:
        for i, c in enumerate(corrections):
            if not isinstance(c, dict) or not CORRECTION_KEYS <= set(c):
                bad.append(f"correction[{i}] missing keys {sorted(CORRECTION_KEYS)}")
    return bad


def speaking_violations(res: dict) -> list[str]:
    bad: list[str] = []
    if not isinstance(res, dict):
        return ["response is not an object"]
    for key in ("scorePercent", "accuracyVsTarget"):
        v = res.get(key)
        if not isinstance(v, (int, float)):
            bad.append(f"{key} missing or not a number")
        elif not 0 <= float(v) <= 100:
            bad.append(f"{key} out of range: {v}")
    if not isinstance(res.get("transcript"), str):
        bad.append("transcript missing or not a string")
    if not isinstance(res.get("pronunciationTips"), list):
        bad.append("pronunciationTips is not a list")
    return bad


# ---------------- quality ----------------

def _flagged(phrase: str, corrections: list[dict]) -> bool:
    """Did the grader flag this planted error? Accepts an exact hit in any correction's
    `original`, or a strong token overlap (graders often quote a slightly wider span)."""
    want = _tokens(phrase)
    if not want:
        return False
    joined = " | ".join(_norm(c.get("original", "")) + " " + _norm(c.get("correction", ""))
                        for c in corrections if isinstance(c, dict))
    if _norm(phrase) in joined:
        return True
    for c in corrections:
        if not isinstance(c, dict):
            continue
        got = set(_tokens(c.get("original", "")))
        if got and len(got & set(want)) / len(want) >= 0.6:
            return True
    return False


def writing_case(case: dict, res: dict) -> dict:
    exp = case.get("expected", {})
    violations = writing_violations(res)
    lo, hi = exp.get("score_band", [0, 100])
    score = res.get("scorePercent") if isinstance(res.get("scorePercent"), (int, float)) else None
    mid = (lo + hi) / 2

    corrections = res.get("corrections") if isinstance(res.get("corrections"), list) else []
    got_cats = {str(c.get("category", "")).lower() for c in corrections if isinstance(c, dict)}
    want_cats = [c.lower() for c in exp.get("categories", [])]
    want_flags = exp.get("must_flag", [])

    est = res.get("cefrEstimate")
    exp_cefr = exp.get("cefr")
    cefr_exact = est == exp_cefr
    cefr_adjacent = False
    if est in LEVELS and exp_cefr in LEVELS:
        cefr_adjacent = abs(LEVELS.index(est) - LEVELS.index(exp_cefr)) <= 1

    return {
        "id": case["id"],
        "contract_ok": not violations,
        "violations": violations,
        "score": score,
        "in_band": score is not None and lo <= score <= hi,
        "abs_err": abs(score - mid) if score is not None else None,
        "cefr_exact": cefr_exact,
        "cefr_adjacent": cefr_adjacent,
        # A clean text has no expected categories/flags; those cases score 1.0 by convention
        # so they don't drag the averages, but they still count for contract and band.
        "category_recall": (sum(1 for c in want_cats if c in got_cats) / len(want_cats)) if want_cats else None,
        "flag_recall": (sum(1 for f in want_flags if _flagged(f, corrections)) / len(want_flags)) if want_flags else None,
        "false_alarm": bool(not want_cats and not want_flags and len(corrections) > 0),
    }


def speaking_case(case: dict, res: dict) -> dict:
    exp = case.get("expected", {})
    violations = speaking_violations(res)
    lo, hi = exp.get("score_band", [0, 100])
    score = res.get("scorePercent") if isinstance(res.get("scorePercent"), (int, float)) else None
    return {
        "id": case["id"],
        "contract_ok": not violations,
        "violations": violations,
        "score": score,
        "in_band": score is not None and lo <= score <= hi,
        "abs_err": abs(score - (lo + hi) / 2) if score is not None else None,
    }


# ---------------- aggregation ----------------

def _mean(values: list) -> float | None:
    vals = [v for v in values if v is not None]
    return round(sum(vals) / len(vals), 4) if vals else None


def aggregate(writing_rows: list[dict], speaking_rows: list[dict]) -> dict:
    clean = [r for r in writing_rows if r["category_recall"] is None and r["flag_recall"] is None]
    return {
        "writing": {
            "cases": len(writing_rows),
            "contract_valid": _mean([1.0 if r["contract_ok"] else 0.0 for r in writing_rows]),
            "score_in_band": _mean([1.0 if r["in_band"] else 0.0 for r in writing_rows]),
            "score_mae": _mean([r["abs_err"] for r in writing_rows]),
            "cefr_exact": _mean([1.0 if r["cefr_exact"] else 0.0 for r in writing_rows]),
            "cefr_adjacent": _mean([1.0 if r["cefr_adjacent"] else 0.0 for r in writing_rows]),
            "category_recall": _mean([r["category_recall"] for r in writing_rows]),
            "error_flag_recall": _mean([r["flag_recall"] for r in writing_rows]),
            "false_alarm_rate": _mean([1.0 if r["false_alarm"] else 0.0 for r in clean]) if clean else None,
        },
        "speaking": {
            "cases": len(speaking_rows),
            "contract_valid": _mean([1.0 if r["contract_ok"] else 0.0 for r in speaking_rows]),
            "score_in_band": _mean([1.0 if r["in_band"] else 0.0 for r in speaking_rows]),
            "score_mae": _mean([r["abs_err"] for r in speaking_rows]),
        },
    }
