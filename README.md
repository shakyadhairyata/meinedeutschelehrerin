# MeineDeutscheLehrerin

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
  plus practice sets and mock exams.
- 12 exercise types, all graded server-side: multiple choice, fill-in-the-blank, cloze, reorder,
  matching, reading and listening comprehension, dictation, conjugation, translation, and
  AI-scored writing and speaking.
- Listening and speaking work in the browser: text-to-speech reads the listening clips, and the
  Web Speech API transcribes spoken answers on the device.
- Spaced-repetition vocabulary (Leitner boxes) with 1,100+ words across the five levels.
- A dashboard with streaks, per-skill accuracy, weakest grammar topics, and level completion.
- A generated two-week study plan per level.

## Stack

- **Frontend** — React 19, Vite, Tailwind.
- **API** — ASP.NET Core (.NET 10) with EF Core and ASP.NET Identity. SQLite for local dev,
  PostgreSQL in production.
- **Language service** — Python/FastAPI backed by Claude for writing and speaking feedback. If
  no API key is set it falls back to deterministic scoring, so the app stays usable offline.

```
React (Vite)  ->  ASP.NET Core API  ->  FastAPI language service  ->  Claude
                        |
                        v
                 SQLite / PostgreSQL
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
```

The exercise importer is self-validating: it grades each answer key before inserting it, so a
wrong key is skipped instead of being stored.

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

## License

[MIT](LICENSE).
