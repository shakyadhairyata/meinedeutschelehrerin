# Retrieval knob map — curriculum RAG

Every parameter that affects what `retriever.search()` returns, traced through the three stages.
Current values are what the pipeline ships with today.

## Stage 1 — Ingestion / chunking (`app/rag/retriever.py`)

| Knob | Symbol | Current | Effect | Re-index? |
|---|---|---|---|---|
| Chunk size | `MAX_CHARS` (L18) | 700 | paragraphs are packed up to this length; longer paragraphs are hard-split | yes |
| Min chunk length | `MIN_CHARS` (L19) | 40 | chunks shorter than this are dropped | yes |
| Overlap | — (`_split`) | **none** | packs whole paragraphs; no sliding-window overlap between chunks | yes |
| Chunk identity | `_chunk_id` | level+source+title+ordinal+text | dedup key; the same text at two levels stays two chunks | yes |
| Heading prefix | `build_chunks` | title `(topic)` prepended to each chunk's text | topic terms get embedded, boosting topical recall | yes |

## Stage 2 — Embedding (`app/rag/embeddings.py`)

| Knob | Symbol | Current | Effect | Re-index? |
|---|---|---|---|---|
| Embedder | Voyage vs hashing | hashing (no key) | provider; **eval uses hashing (offline, deterministic)** | yes |
| Vector width | `DIM` (L18) | 512 | hash buckets; smaller = more collisions | yes |
| Char n-grams | `_tokens` (L38) | word + 4-grams (words len>4) | 4-grams keep German compounds/inflections close | yes |
| Term weighting | `fit_idf` / `_hash_embed` | sublinear TF × **IDF** | rare, discriminative terms outweigh ubiquitous ones | yes |
| Query/index space | `embed_query_for` | query embedded in the index's space | prevents cross-space noise | — |

## Stage 3 — Query / ranking (`app/rag/retriever.py::search`)

| Knob | Symbol | Current | Effect | Re-index? |
|---|---|---|---|---|
| top-k | `k` param | caller (grammar-help: 3–4) | how many results returned | no |
| Candidate pool | `max(k*5, 20)` (L141) | 20+ | how many the reranker sees | no |
| Level handling | rank bonus, not filter | preference | a rule taught at another level is still reachable | no |
| Level boost | `LEVEL_BOOST` (L23) | 0.05 | added to same-level hits' score | no |
| Source boost | `SOURCE_BOOST` (L24) | 0.05 | added to `lesson` (explanation) hits over `exercise` | no |
| Relevance floor | `MIN_SCORE` (L21) | 0.20 | hits below this are dropped (→ can cause zero-results) | no |
| Reranking | `rank()` (L144) | `cosine + level_boost + source_boost` | linear re-score of the pool | no |

`MIN_SCORE`, `LEVEL_BOOST`, `SOURCE_BOOST` are overridable at runtime via env
(`RAG_MIN_SCORE`, `RAG_LEVEL_BOOST`, `RAG_SOURCE_BOOST`); the rest are code constants.

## Knobs this harness sweeps (offline, one at a time)
`MAX_CHARS`, `MIN_SCORE`, `LEVEL_BOOST`, `SOURCE_BOOST`, `DIM`, IDF on/off, 4-grams on/off.
(Voyage embedder is excluded from the sweep — it needs a network + key and isn't reproducible in CI.)
