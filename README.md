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
- Calendar (month grid, workouts + runs, tap a day for detail)
- Per-exercise stats page: progression chart, PRs (heaviest weight, est. 1RM, best set/session volume), paginated session history
- Muscle-group tagging per exercise (primary + secondary) with a weekly Muscle Balance page: bar chart + front/back heatmap
- Running session logging (distance + duration)
- Historical data imported from a Hevy CSV export (76 sessions, notes, tempo backfilled from the program doc)

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

## Deploying

On the NAS (`/mnt/tank/callahan`):
```bash
sudo git pull && sudo docker compose -f docker-compose.prod.yml up -d --build
```
EF Core migrations apply automatically on backend startup. Direct DB changes (data backfills, program syncs) follow the procedure in `docs/program-sync.md`.

## Docs
- `docs/program-sync.md` — how program/template changes get applied (there's no in-app editor by design)
- `.ui-craft/brief.md` — design brief and learned UI constraints
