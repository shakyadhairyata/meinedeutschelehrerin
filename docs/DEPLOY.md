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

## Verifying

- API health: `https://<api-url>/api/health` → `{"status":"ok"}`
- Language service: `https://<ai-url>/health` → `{"status":"ok"}`
- App: open the static site URL, register, and work through a lesson.
