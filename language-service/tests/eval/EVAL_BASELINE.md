# Curriculum RAG — retrieval eval baseline & tuning sweep

Harness: `tests/eval/harness.py` over 30 golden queries (`golden_queries.yaml`) against the
real curriculum corpus (`corpus.json`, 406 docs, A1–C1). Offline: in-memory store + the
deterministic hashing embedder, no DB, no network. A query "hits" if a top-5 result's title
or grammar topic is in its `expect` list.

## Baseline (shipped config)
`max_chars=700, min_score=0.20, level_boost=0.05, source_boost=0.05, dim=512, IDF on, 4-grams on`

| metric | value |
|---|---|
| recall@1 | 0.933 |
| recall@5 | **0.933** |
| MRR | 0.933 |
| zero-results rate | **0.000** |
| hard recall@5 | 0.600 (3/5) |
| misses | q13 (als/wenn), q14 (Wechselpräpositionen B1→A2) |

Every hit lands at rank 1 (recall@1 == recall@5) — when retrieval finds the right lesson it
ranks it first; the only failures are total misses. Baseline already clears the 0.9 target and
the 0.8 gate with zero empty results.

## Sweep — one knob changed per run (12 configs)

Ranked by recall@5, then MRR:

| rank | config | R@1 | R@5 | MRR | hard@5 | misses | one-line result |
|---|---|---|---|---|---|---|---|
| 1 | **C1 IDF off** | 0.967 | **0.967** | 0.967 | 0.8 | q14 | fixes als/wenn — the topic *is* function words that IDF was downweighting; no regressions |
| 1 | C10 dim 256 | 0.967 | 0.967 | 0.967 | 0.8 | q14 | also fixes q13, but via extra hash collisions — looks coincidental, not a principled win |
| 3 | C11 min_score 0.0 | 0.933 | 0.967 | 0.950 | 0.8 | q14 | q13's correct hit was scored below the 0.20 floor; dropping the floor surfaces it (but removes the off-topic guard) |
| 4 | **C0 baseline** | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | reference |
| 4 | C3 chunk 400 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect — lessons are short; chunking rarely splits them |
| 4 | C4 chunk 1000 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect |
| 4 | C5 level_boost 0.0 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect on recall (q14 still loses on relevance, not level) |
| 4 | C7 source_boost 0.0 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect |
| 4 | C8 source_boost 0.10 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect |
| 4 | C9 dim 1024 | 0.933 | 0.933 | 0.933 | 0.6 | q13,q14 | no effect — 512 already low-collision for this corpus |
| 11 | C6 level_boost 0.10 | 0.900 | 0.933 | 0.917 | 0.6 | q13,q14 | mild regression: over-boosting the same level demotes a correct rank-1 |
| 12 | C2 4-grams off | 0.833 | 0.933 | 0.869 | 1.0 | q01,q24 | **regression**: fixes both hard cases but breaks two easy ones (q01,q24) and tanks ranking — n-grams matter for compounds |

## The one case no knob fixes — q14
"Brauche ich Dativ oder Akkusativ nach in, an und auf?" should retrieve *Wechselpräpositionen:
Wo? vs. Wohin?* (A2), but the query's terms (Dativ, Akkusativ, in/an/auf) match the generic
*Der Dativ* / *Der Akkusativ* lessons at least as strongly. This is a **semantic** miss: a
lexical hashing embedder can't know those two cases collapse to one rule. No chunk-size,
weighting, or boost knob closes it — it needs real embeddings (Voyage) or query/topic expansion.

## Recommendation
- **The shipped config is sound** (0.933, no empty results) and IDF is the right default for a
  corpus that will keep growing — I did **not** flip it in production off a 30-query sample.
- If optimizing purely for this golden set, **`RAG_*`-tunable IDF-off is the single best knob**
  (C1, 0.933→0.967) with no regressions — but the gain is one query (als/wenn), the classic
  IDF failure mode. I'd adopt it only alongside a larger golden set. C10/C11 reach the same
  number less defensibly (collision luck / losing the off-topic floor).
- **The real headroom is the embedder, not these lexical knobs.** q14 (and getting past ~0.97)
  needs Voyage/semantic retrieval; the sweep shows lexical tuning has essentially topped out here.
- Keep this harness as a CI gate (recall@5 ≥ 0.8, zero-results = 0) so any future change to
  chunking/embedding/ranking is caught.
