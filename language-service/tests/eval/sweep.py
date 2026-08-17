"""Single-knob tuning sweep over the retrieval harness.

Starts from the shipped config and changes exactly ONE knob per run, so every delta is
attributable. Prints a table; the agent reads it, keeps wins and reverts regressions.
"""
import json

from harness import DEFAULT_CONFIG, evaluate  # run from tests/eval/


def _cfg(**overrides):
    c = dict(DEFAULT_CONFIG)
    c.update(overrides)
    return c


CONFIGS = [
    ("C0  baseline (shipped)",        _cfg()),
    ("C1  IDF off",                   _cfg(use_idf=False)),
    ("C2  4-grams off (words only)",  _cfg(use_ngrams=False)),
    ("C3  chunk 400",                 _cfg(max_chars=400)),
    ("C4  chunk 1000",                _cfg(max_chars=1000)),
    ("C5  level_boost 0.0",           _cfg(level_boost=0.0)),
    ("C6  level_boost 0.10",          _cfg(level_boost=0.10)),
    ("C7  source_boost 0.0",          _cfg(source_boost=0.0)),
    ("C8  source_boost 0.10",         _cfg(source_boost=0.10)),
    ("C9  dim 1024",                  _cfg(dim=1024)),
    ("C10 dim 256",                   _cfg(dim=256)),
    ("C11 min_score 0.0",             _cfg(min_score=0.0)),
]


def main():
    print(f"{'config':<28}{'R@1':>7}{'R@5':>7}{'MRR':>7}{'zero':>7}{'hard@5':>8}  misses")
    for name, cfg in CONFIGS:
        m = evaluate(cfg)
        print(f"{name:<28}{m['recall@1']:>7}{m['recall@5']:>7}{m['mrr']:>7}"
              f"{m['zero_results_rate']:>7}{str(m['hard_recall@5']):>8}  {','.join(m['misses']) or '-'}")


if __name__ == "__main__":
    main()
