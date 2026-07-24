# Deploying to Render

The repo includes a [`render.yaml`](../render.yaml) Blueprint that stands up four things:

| Service | Type | Notes |
|---------|------|-------|
| `meinedeutschelehrerin-db` | PostgreSQL | Free plan. Schema is created and seeded on the API's first boot. |
| `meinedeutschelehrerin-api` | Docker web service | The .NET API. Listens on Render's `$PORT`. |
| `meinedeutschelehrerin-ai` | Docker web service | The Python language service. Optional — the app works without it. |
| `meinedeutschelehrerin` | Static site | The React build. Always on (static sites don't sleep). |

## Steps

1. Push this repo to GitHub (see the root README for the one-time setup).
2. Go to <https://dashboard.render.com/blueprints> and click **New Blueprint Instance**.
3. Select the repository. Render reads `render.yaml` and lists the four resources.
4. Click **Apply**. First build takes a few minutes (Docker images + the frontend build).

That's it — the static site URL is your live app.

## After the first deploy

The Blueprint wires the services together by URL, assuming the names above are free. If Render
appends a suffix because a name was taken (e.g. `meinedeutschelehrerin-api-xy12`), update these
three values in the dashboard so they point at the real URLs:

- API service → `Cors__AllowedOrigins__0` (the static site's URL)
- API service → `LanguageService__BaseUrl` (the language service's URL)
- Static site → `VITE_API_BASE_URL` (the API's URL), then redeploy the static site so the new
  value is baked into the build.

## Real AI feedback (optional)

Writing and speaking fall back to deterministic scoring with no key. To turn on Claude-generated
feedback, set `ANTHROPIC_API_KEY` on the `meinedeutschelehrerin-ai` service and redeploy it.

## Free-tier notes

- The two Docker web services **sleep after ~15 minutes idle** and cold-start in 30–60s. The
  first request after a nap is slow; the static frontend stays instant.
- Render's **free PostgreSQL expires after 90 days**. Create a fresh one (or upgrade) before then;
  the API re-creates the schema and re-seeds on boot.
- Email confirmation is off (`Identity__RequireConfirmedEmail=false`) so anyone can register and
  sign in immediately. Turn it on and configure SMTP (`Email__*`) for a real launch.

## Database & schema changes

The API applies **EF Core migrations** on startup for both providers (SQLite migrations live in
`Infrastructure`, PostgreSQL migrations in the `Migrations.Postgres` project). So new schema is
applied automatically on each deploy — no manual step for normal changes.

**One-time transition:** the app previously used `EnsureCreated` for Postgres, which doesn't record
a migrations history. To move an existing prod database onto migrations, **recreate the free
Postgres once** (set `Admin__Email`/`Admin__Password` first). The API re-seeds curriculum, content,
feature flags and the admin on first boot. After that, schema changes apply cleanly with no data loss.

## Verifying

- API health: `https://<api-url>/api/health` → `{"status":"ok"}`
- Language service: `https://<ai-url>/health` → `{"status":"ok"}`
- App: open the static site URL, register, and work through a lesson.

## Grammar RAG & grader evals

**Grammar help** (`/api/grammar-help`) answers "why is this wrong?" from the app's *own*
lessons rather than the model's general knowledge, so every explanation is traceable to
material the learner has been taught.

- The vector index lives in the Python language-service. The .NET API owns the corpus (it has
  the curriculum DB) and pushes it over on startup; an admin can rebuild it any time via
  `POST /api/grammar-help/reindex`.
- Storage is **pgvector** in the existing Postgres (`RAG_DATABASE_URL`), so the index survives
  free-tier restarts. Without a database URL it falls back to an in-memory index.
- Embeddings use **Voyage** when `VOYAGE_API_KEY` is set, otherwise a deterministic
  TF-IDF hashing embedder — so retrieval works with no keys at all (CI, local dev).
- Retrieval is free and ungated. Only the optional Claude summary spends tokens, and it goes
  through the same `IAiAccessService` quota as the other AI features.
- Level is a ranking *preference*, not a filter: the rule a learner needs is often taught at a
  different level than the exercise they are stuck on.

**Grader evals** measure the writing/speaking grader against a labelled German golden set:

```bash
cd language-service
python -m evals.run                 # report
python -m evals.run --check         # fail on regression vs baseline
python -m evals.run --save-baseline # re-record the baseline
```

Contract validity (well-formed, bindable responses) is enforced in CI with no API key.
Quality metrics are only compared against a baseline recorded in the **same mode**, because
the offline fallback is legitimately far weaker than Claude — quantifying that gap is the
point of the suite.

## Study Coach (multi-agent, LangGraph)

`POST /api/coach/turn` runs a multi-agent tutor built with **LangGraph** (supervisor pattern):
a `planner` turns the learner's message into a plan; a `supervisor` routes to specialist agents
— `grammar_explainer` (calls the RAG tool), `exercise_generator`, `evaluator` (grades) — that
coordinate over shared state.

- **Memory**: a LangGraph checkpointer keyed by user id. Postgres in production
  (`COACH_DATABASE_URL`, same DB) so memory survives restarts; in-memory otherwise. The coach
  grades the exercise it set on a previous turn and remembers weak topics.
- **Feature flag**: `study_coach`. **Cost control**: LLM enhancement is billed through
  `IAiAccessService` (Paid + daily quota); Free/over-quota users still get the fully functional
  deterministic coach (retrieval, exercises, grading cost no tokens).
- **Observability (LLMOps)**: every turn reports latency, LLM call count, token usage and the
  prompt versions used (from a versioned prompt registry). Set `LANGSMITH_TRACING=true` and
  `LANGSMITH_API_KEY` to stream full run traces to LangSmith, grouped by prompt version.
  `GET /coach/health` reports whether tracing is on.

```bash
cd language-service && pytest tests/test_agent.py -q   # keyless: 12 tests
```
