# MeineDeutscheLehrerin

**Live demo → [meinedeutschelehrerin.onrender.com](https://meinedeutschelehrerin.onrender.com)**

A German learning platform that takes you from A1 to C1 and helps you prepare for the
Goethe-Institut exams. Each level is laid out as a two-week course (one themed unit per study
day) and covers all six skills: grammar, vocabulary, reading, listening, speaking and writing.
Exercises are graded instantly, vocabulary uses spaced repetition, and a study plan tells you
what to work on each day.

I'm working toward the Goethe exams myself and couldn't find a single app that combined a
structured syllabus with real exercise grading and progress tracking, so I built one.

## What it does

- Email accounts with isolated per-user progress (ASP.NET Identity, bearer tokens).
- A full A1–C1 syllabus modelled as Level → Unit (a study day) → Lesson (per skill) → Exercise,
  plus themed practice sets.
- 12 exercise types, all graded server-side: multiple choice, fill-in-the-blank, cloze, reorder,
  matching, reading and listening comprehension, dictation, conjugation, translation, and
  AI-scored writing and speaking.
- Full-length Goethe mock exams (A1–B1) built as the four official modules — Lesen, Hören,
  Schreiben and Sprechen — each separately timed and auto-advancing when its clock runs out,
  with a per-module score at the end.
- Grammar help that answers "why is this wrong?" from the app's *own* lessons rather than the
  model's general knowledge: a small vector index over the lesson explanations and exercise
  rationales, so every answer cites the material it came from.
- A multi-agent **Study Coach** (built with LangGraph): a planner plus grammar/exercise/evaluator
  agents that coordinate over the app's own tools and remember the session per learner (it grades
  the exercise it set last turn), with a Postgres checkpointer for per-thread memory.
- A **provider-agnostic LLM layer**: every model call — grading, generation, grammar answers and
  the coach — routes through a gateway that tries Anthropic → OpenAI → a local open-source model
  (Ollama), falls back on failure or rate limits, and records tokens, latency and estimated cost
  per call. Runs are traced to **LangSmith** and tagged with their prompt version, and each coach
  turn returns its own LLMOps metrics.
- Listening and speaking work in the browser: text-to-speech reads the listening clips, and the
  Web Speech API transcribes spoken answers on the device.
- Spaced-repetition vocabulary (Leitner boxes) with 1,100+ words across the five levels.
- A dashboard with streaks, per-skill accuracy, weakest grammar topics, and level completion.
- A generated two-week study plan per level.
- An admin panel to toggle features on and off and switch users between Free and Paid tiers.
  AI-graded feedback and the grammar summary are gated by tier and a daily quota, so the
  Anthropic budget can't be drained — everyone still gets deterministic scoring and the cited
  explanations for free.

## Stack

- **Frontend** — React 19, Vite, Tailwind.
- **API** — ASP.NET Core (.NET 10) with EF Core and ASP.NET Identity. SQLite for local dev,
  PostgreSQL in production.
- **Language service** — Python/FastAPI hosting the LangGraph multi-agent coach, the writing/
  speaking graders, content generation, and the grammar retrieval index. All LLM access goes
  through a multi-provider **gateway** (LangChain-backed) with fallback routing and cost/latency
  accounting; **LangSmith** tracing and prompt versioning provide observability. With no keys it
  falls back to deterministic scoring and an offline embedder, so the app stays fully usable
  offline (and CI runs keyless).
- **Retrieval** — the grammar index lives in the language service: pgvector in production (in the
  same Postgres, so it survives restarts), an in-memory index in dev. Embeddings use Voyage when
  `VOYAGE_API_KEY` is set, otherwise a deterministic TF-IDF hashing embedder; the index is always
  labeled with the embedder actually used, and queries embed in that same space.

```
React (Vite)  ->  ASP.NET Core API  ->  FastAPI language service  ->  LLM gateway  ->  Anthropic / OpenAI / Ollama
                        |                        |                          |
                        v                        v                          v
                 SQLite / PostgreSQL      grammar index (pgvector)     LangSmith (tracing)
```

The .NET solution is split into three projects: `Domain` (entities, enums and DTO contracts),
`Infrastructure` (the EF Core context, services, the language-service HTTP client and the
curriculum seeder), and `Api` (controllers and `Program.cs` wiring).

## Running it locally

You need the .NET 10 SDK, Node 20+, and Python 3.10–3.12.

```bash
# API — creates the SQLite database and seeds the curriculum on first run
dotnet run --project src/MeineDeutscheLehrerin.Api --urls http://localhost:5099

# Language service (optional; the app runs without it)
cd language-service
python -m venv .venv
.venv/Scripts/pip install -r requirements.txt      # .venv/bin on macOS/Linux
.venv/Scripts/uvicorn app.main:app --port 8001

# Frontend — Vite proxies /api to :5099
cd frontend && npm install && npm run dev
```

Open http://localhost:5173, register with any email and a password (at least 8 characters with a
digit and mixed case), and start learning.

Or bring everything up with Docker:

```bash
cp .env.example .env        # add ANTHROPIC_API_KEY here for real AI feedback
docker compose up --build
```

## Content

The curriculum is seeded from `Infrastructure/Seeding/DbSeeder.cs` and seeding is idempotent per
level, so adding a level doesn't wipe existing data. Vocabulary and extra exercises also live as
editable JSON under `content/` and are imported through the CLI:

```bash
dotnet run --project src/MeineDeutscheLehrerin.Api -- import-vocab all ./content/vocabulary
dotnet run --project src/MeineDeutscheLehrerin.Api -- import-exercises all ./content/exercises
dotnet run --project src/MeineDeutscheLehrerin.Api -- import-exams all ./content/exams
```

The importers are self-validating: they grade each auto-gradable answer key before inserting it,
so a wrong key is skipped instead of being stored. The Goethe mock exams under `content/exams`
are hand-authored at the official item counts and per-module durations.

## Deployment

The repo ships a `render.yaml` blueprint that provisions the API, the language service, a
managed PostgreSQL database and the static frontend on [Render](https://render.com). See
[docs/DEPLOY.md](docs/DEPLOY.md) for the step-by-step. Each service also has a `Dockerfile`, and
`docker compose up` runs the whole stack locally.

## Tests

```bash
dotnet test                        # backend (xUnit)
cd frontend && npm test            # frontend (Vitest)
cd language-service && pytest -q    # language service (pytest)
```

The backend suite includes a seed-consistency test that grades every authored exercise's own
answer key, so a broken key anywhere in the A1–C1 content fails the build. GitHub Actions runs
all three suites on every push.

The AI grader has its own eval suite against a labelled German golden set — score accuracy, CEFR
estimation and planted-error recall — with a committed baseline so quality can't silently
regress:

```bash
cd language-service
python -m evals.run            # report the current numbers
python -m evals.run --check    # fail if metrics regress past the baseline
```

Response-shape checks run keyless in CI; the live-Claude checks are skipped unless an API key is
present. See [docs/DEPLOY.md](docs/DEPLOY.md) for the grammar-help and eval details.

## License

[MIT](LICENSE).
