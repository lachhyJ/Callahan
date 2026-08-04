# RepLog

Self-hosted training tracker — gym workouts and running sessions, replacing Hevy's subscription, with Garmin readiness/sleep data folded in later.

Full roadmap and decisions: `~/moxie-vault/30-projects/replog/overview.md`

## Stack
- Backend: C# ASP.NET Core Web API (.NET 10), EF Core + SQLite
- Frontend: React (Vite)
- Hosting (later): Docker on the NAS, behind a Cloudflare tunnel

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

## Status
Phase 0 (scaffolding) — backend/frontend skeleton proven end-to-end, both locally and via Docker. No real features yet.
