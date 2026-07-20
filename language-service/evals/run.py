"""Grader eval runner.

    python -m evals.run                 # evaluate, print a report
    python -m evals.run --check         # also fail if metrics regress past the baseline
    python -m evals.run --save-baseline # record the current numbers as the baseline

Runs against whatever the grader currently is: with ANTHROPIC_API_KEY set it measures
Claude, without one it measures the deterministic fallback. The mode is recorded in the
report so live and offline numbers are never compared to each other.
"""
import argparse
import json
import pathlib
import sys
import time

from app import claude_client
from app.evaluator import evaluate_speaking, evaluate_writing
from app.schemas import SpeakingRequest, WritingRequest

from . import metrics

ROOT = pathlib.Path(__file__).parent
DATA = ROOT / "data"
RESULTS = ROOT / "results"
BASELINE = ROOT / "baseline.json"

# Quality may drift a little run-to-run with a live model; contract validity may not. Rates are
# on a 0-1 scale; score_mae is on a 0-100 scale, so its tolerance is in points, not the 0.10
# default (which would fail CI on sub-percent MAE noise or a one-word dataset tweak).
DEFAULT_TOLERANCE = 0.10
TOLERANCE = {
    "contract_valid": 0.0,
    "score_in_band": 0.10,
    "cefr_exact": 0.10,
    "cefr_adjacent": 0.10,
    "category_recall": 0.10,
    "error_flag_recall": 0.10,
    "false_alarm_rate": 0.10,
    "score_mae": 3.0,
}
# Metrics where lower is better.
LOWER_IS_BETTER = {"score_mae", "false_alarm_rate"}


def load(name: str) -> list[dict]:
    with open(DATA / name, encoding="utf-8") as fh:
        return [json.loads(line) for line in fh if line.strip()]


def mode() -> str:
    return "live" if claude_client.is_enabled() else "offline"


def run() -> dict:
    writing_rows, speaking_rows = [], []

    for case in load("writing.jsonl"):
        res = evaluate_writing(WritingRequest(
            prompt=case["prompt"], text=case["text"],
            level=case["level"], min_words=case["min_words"],
        ))
        writing_rows.append(metrics.writing_case(case, res))

    for case in load("speaking.jsonl"):
        res = evaluate_speaking(SpeakingRequest(
            target_text=case["target_text"], transcript=case["transcript"], level=case["level"],
        ))
        speaking_rows.append(metrics.speaking_case(case, res))

    return {
        "mode": mode(),
        "model": claude_client.DEFAULT_MODEL if mode() == "live" else "offline-heuristic",
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "summary": metrics.aggregate(writing_rows, speaking_rows),
        "writing": writing_rows,
        "speaking": speaking_rows,
    }


def _fmt(v) -> str:
    return "  n/a" if v is None else f"{v:>5.3f}"


def report(result: dict) -> None:
    print(f"\nGrader eval — mode={result['mode']} model={result['model']}")
    for section in ("writing", "speaking"):
        print(f"\n  {section}:")
        for key, value in result["summary"][section].items():
            print(f"    {key:<20} {_fmt(value) if isinstance(value, float) else value}")

    failures = [r for r in result["writing"] + result["speaking"] if not r["contract_ok"]]
    if failures:
        print("\n  contract violations:")
        for row in failures:
            print(f"    {row['id']}: {'; '.join(row['violations'])}")

    misses = [r for r in result["writing"] if not r["in_band"]]
    if misses:
        print("\n  outside expected score band:")
        for row in misses:
            print(f"    {row['id']}: scored {row['score']}")


def check(result: dict) -> int:
    """Compare against the committed baseline for the same mode."""
    if not BASELINE.exists():
        print("\nNo baseline recorded — run with --save-baseline first.")
        return 0
    baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
    if baseline.get("mode") != result["mode"]:
        print(f"\nBaseline is '{baseline.get('mode')}' but this run is '{result['mode']}' — skipping check.")
        return 0

    regressions = []
    for section, values in result["summary"].items():
        for key, now in values.items():
            before = baseline["summary"].get(section, {}).get(key)
            if not isinstance(now, float) or not isinstance(before, float):
                continue
            tol = TOLERANCE.get(key, DEFAULT_TOLERANCE)
            worse = (now > before + tol) if key in LOWER_IS_BETTER else (now < before - tol)
            if worse:
                regressions.append(f"{section}.{key}: {before:.3f} -> {now:.3f}")

    if regressions:
        print("\nREGRESSION vs baseline:")
        for r in regressions:
            print(f"  {r}")
        return 1
    print("\nNo regression vs baseline.")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="Evaluate the writing/speaking grader.")
    ap.add_argument("--check", action="store_true", help="fail on regression vs baseline")
    ap.add_argument("--save-baseline", action="store_true", help="record this run as the baseline")
    ap.add_argument("--json", action="store_true", help="print raw JSON instead of a report")
    args = ap.parse_args()

    result = run()
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        report(result)

    RESULTS.mkdir(exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    (RESULTS / f"{result['mode']}-{stamp}.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")

    if args.save_baseline:
        BASELINE.write_text(json.dumps(
            {"mode": result["mode"], "model": result["model"], "summary": result["summary"]},
            ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"\nBaseline saved ({result['mode']}).")

    return check(result) if args.check else 0


if __name__ == "__main__":
    sys.exit(main())
