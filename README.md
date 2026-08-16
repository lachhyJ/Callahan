# Callahan

Self-hosted training tracker — gym workouts and running sessions, replacing Hevy's history paywall. Live at `callahan.ljlab.online`.

## Stack
- Backend: C# ASP.NET Core Web API (.NET 10), EF Core + SQLite
- Frontend: React (Vite), plain CSS with a token spine (`frontend/src/index.css`)
- Auth: single credential + JWT (deliberately not ASP.NET Identity — single-user app)
- Hosting: Docker Compose on a home NAS, behind a Cloudflare tunnel

## Features
- Workout templates (3-day program, seeded from a program PDF) with per-exercise target sets/reps/rest/tempo and a persistent per-slot "cue" note
- Active workout flow: tick-to-complete sets, warmup/failure/drop set types, rest timers with background push notifications, per-session exercise notes
- Finishers (optional bonus exercises), live session duration/volume header
- History list and a full per-session detail page
- Dashboard: month calendar grid (workouts + runs, tap a day for a bottom-sheet detail view) plus a quick-links grid to Muscle Balance, Exercises, Streaks, Trends, and Program
- Streaks: three weekly consistency definitions (2+ gym sessions, 3+ total sessions, 3 gym + a run), each with current/best run length; drill into a week to see what was actually logged and whether it qualified
- Trends: consistency and volume charted over 6 months (vs. the Dashboard's 8-week glance chart), plus a "Lift trends" list of the exercises with the biggest earliest-vs-latest movement in that window
- Program: the training PDF (sourced from Nextcloud, not the repo) opened via the browser's own PDF viewer
- Per-exercise stats page: progression chart, PRs (heaviest weight, est. 1RM, best set/session volume), paginated session history
- Muscle-group tagging per exercise (primary + secondary) with a weekly Muscle Balance page: bar chart + front/back heatmap
- Running session logging (distance + duration), launched from a button on the Workout page
- Session display names: the workout template's name where one's associated, otherwise a summary of the exercise categories actually done (Push/Pull/Legs/etc.) — most imported history predates any template association
- Historical data imported from a Hevy CSV export (76 sessions, notes/tempo backfilled from the program doc; template association backfilled separately from Apr 28, 2026 onward, once the program had settled into its current form)

## Running locally (without Docker)

Backend:
```bash
cd backend
dotnet run --urls http://localhost:5080
```

Frontend:
```bash
cd frontend
npm install
npm run dev
```
Frontend expects the backend at `http://localhost:5080` by default (`VITE_API_BASE` env var to override).

## Running locally with Docker

```bash
docker compose up --build
```
- Backend: http://localhost:8080
- Frontend: http://localhost:8081

`docker-compose.yml` (this local-dev file, not `docker-compose.prod.yml`) sets
`ASPNETCORE_ENVIRONMENT=Development`, which enables `POST /api/auth/dev-login` —
see "Verifying UI changes without a password" below.

## Verifying UI changes without a password

`POST /api/auth/dev-login` issues a real JWT for the app's single user with no
password check. It only exists when `ASPNETCORE_ENVIRONMENT=Development` (true for
local Docker/bare-metal runs, false on the NAS — the route 404s in Production, so
it's unreachable on the live deploy regardless of how it's compiled). This lets
tooling (Claude Code's browser preview, etc.) verify authenticated pages without
being handed the real password:
```bash
curl -X POST http://localhost:8080/api/auth/dev-login
# {"token":"..."} — set it as localStorage['callahan_token'] and reload
```

## Deploying

Automatic: `.github/workflows/deploy.yml` deploys to the NAS on every push to `main`
(see `CLAUDE.md` for how it's wired up). To roll back or deploy a specific commit,
run the `Deploy to NAS` workflow manually from the Actions tab with a commit SHA.

EF Core migrations apply automatically on backend startup. Direct DB changes (data backfills, program syncs) follow the procedure in `docs/program-sync.md`.

## Docs
- `docs/program-sync.md` — how program/template changes get applied (there's no in-app editor by design)
- `.ui-craft/brief.md` — design brief and learned UI constraints
