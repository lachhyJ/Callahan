# Architecture

Callahan is a single-user training tracker: gym workouts, running, and Ultimate Frisbee
games, with wellness and activity data pulled from Garmin Connect. It runs as four
containers on a home NAS behind a Cloudflare tunnel, and the same web build also ships as
a native iOS app.

This page describes how the pieces fit together. For *why* they're arranged this way, see
[decisions.md](decisions.md).

## The system at a glance

```
                    ┌──────────────────────┐
   iOS app  ───────▶│                      │
  (Capacitor        │   Cloudflare tunnel  │
   webview)         │                      │
                    └──────────┬───────────┘
   Browser  ───────────────────┤
                               │
                    ┌──────────▼───────────┐
                    │  frontend            │  nginx serving the Vite build
                    │  (nginx + React SPA) │  + security headers per location
                    └──────────┬───────────┘
                               │  /api/*
                    ┌──────────▼───────────┐
                    │  backend             │  ASP.NET Core Web API (.NET 10)
                    │  (ASP.NET Core)      │  EF Core ──▶ SQLite (volume)
                    └───┬──────────────┬───┘
                        │              │
       POST /sync       │              │  Anthropic API
                        │              │  (taper consult only)
              ┌─────────▼─────────┐    └──────────────▶
              │ garmin-sync-      │
              │ trigger (Python)  │────┐
              └───────────────────┘    │
                                       ├──▶ Garmin Connect
              ┌───────────────────┐    │    (unofficial client)
              │ nightly cron      │────┘
              │ (same image,      │
              │  docker run --rm) │
              └───────────────────┘
```

Four running pieces, plus a nightly job:

| Piece | What it is | Notes |
|---|---|---|
| `frontend` | nginx serving the built React SPA | Also owns the CSP and security headers. `connect-src` is substituted at image build time so local dev can reach a different API origin |
| `backend` | ASP.NET Core Web API, EF Core over SQLite | The only thing that touches the database |
| `garmin-sync-trigger` | Python sidecar, stdlib HTTP server | Exists so the "sync now" button doesn't need a Docker socket or a C# reimplementation. Same image as the cron job, different entrypoint |
| nightly cron | `docker run --rm` of the sync image | Pulls activities and wellness on a schedule. Untouched by the sidecar |
| iOS app | Capacitor shell around a webview | Loads the live site rather than bundling the build, so a push updates both targets |

## Request path

A page load hits nginx, which serves the SPA shell and static assets. Every data call goes
to `/api/*`, proxied to the backend.

Auth is a single credential in config plus a JWT, held in `localStorage` and attached by a
shared `apiFetch` wrapper. That wrapper treats any 401 as an expired session and clears the
token — which is why the backend explicitly returns 404, not 401, for unmatched `/api/*`
routes (see [decisions.md](decisions.md#authorization-is-deny-by-default-and-unmatched-api-routes-are-explicitly-404)).

Authorization is deny-by-default via a fallback policy; the auth, health and dev-login
routes opt out explicitly.

## Backend layers

```
Controllers/     21 controllers, one per resource group
   │
   ▼
Services/        the parts worth testing in isolation
   │
   ▼
Data/            AppDbContext ──▶ Migrations/ ──▶ SQLite
```

`Services/` is where the domain logic lives, and it's deliberately mostly pure functions
over data the controller has already fetched. That's what makes the interesting parts unit
testable without a database:

- **`FieldGeometry`** — the GPS analysis. Takes a per-second position track and returns
  on-field / off-field segments, points played, and live-play seconds, by fitting a field
  frame to the game's own fast samples. A line-for-line port of the Python reference
  implementation in `scripts/ultimate-stream-explore/segment.py`.
- **`LapFieldClassifier`** — wraps `FieldGeometry` with the lap-boundary logic and the
  fallback for un-lapped activities. Carries its own version constant, which is what the
  reclassify endpoint gates on.
- **`LiftProgress` / `LiftMath`** — per-exercise progression, including the three-way
  choice of measurement basis (e1RM, set volume, or assisted rank).
- **`MonthlyReportBuilder`** and friends — the deterministic monthly report, including its
  verdict classifier and wellness summary.
- **`ReadinessInsightCalculator`** — a pure function over a wellness day plus its trailing
  28-day window, producing per-metric baseline comparisons.
- **`TaperPhaseCalculator`** — deterministic step-taper percentages. `TaperConsultService`
  is the strictly-additive LLM path alongside it, on a separate code path that can fail
  without affecting the numbers.
- **`WeeklyConsistencyService`** — the three streak definitions, shared server-side so the
  streak page and the dashboard can't drift.

## Data model

SQLite via EF Core. The shape splits roughly three ways:

**Gym.** `WorkoutTemplate` → `WorkoutTemplateExercise` defines the program;
`WorkoutSession` → `ExerciseSet` records what was actually done, with a nullable link back
to the template. `Exercise` carries muscle-group tags and an explicit assisted flag.

**Activities.** `Activity` covers runs and Ultimate games, with `ActivitySessionType` as a
shared classification table across both. Two child entities hang off it: `ActivityLap`
(tabular, aggregated) and `ActivityTrack` (one large columnar JSON blob per game, loaded
only when explicitly included). `Tournament` and `Season` group activities for the
competitive calendar.

**Wellness and derived.** `DailyWellness` holds typed nullable columns per Garmin metric
plus a raw JSON hedge. `MonthlyReport` snapshots a whole report as JSON with a schema
version, rebuilt in place when that version falls behind.

Two storage conventions run through this, and they're deliberate rather than inconsistent:

- A real child table for anything tabular, aggregated or filtered. A JSON blob only for
  data that is read whole, rarely queried, and whose schema is still settling.
- Every `decimal` in the model is mapped to a SQLite REAL column, model-wide, so
  server-side numeric comparison behaves numerically.

## Frontend

React (Vite), plain CSS with a token spine in `index.css` — three layers: primitives,
semantic tokens, then component consumption. Charts are hand-rolled SVG on a shared
pattern rather than a charting library; only the drawing is shared, since the dimensions
genuinely differ per chart.

State lives in a few small modules rather than a store library: `activeWorkout.js` and
`restTimer.js` persist an in-progress session to `localStorage` so it survives a reload,
`persistedSlot.js` generalises that, and `usage.js` records navigation events.

Pure logic — plate maths, date handling, session-type inference, formatting — lives in
plain modules with unit tests. There are no component tests; the defects worth catching
have all been in that pure layer.

`dateUtils.js` is worth calling out. Every date helper here is pure-local or pure-UTC for
its whole parse-manipulate-format round trip, never mixed, and session dates are stamped
with `trainingDayIso` rather than the current clock.

## Native iOS

`frontend/ios/` is a Capacitor shell whose webview points at the live site. It exists for
things a PWA cannot do on iOS:

- A rest beep that plays through the hardware silent switch, ducks music rather than
  stopping it, and fires while backgrounded — an `AVAudioSession` combining `.playback`,
  `.mixWithOthers` and `.duckOthers`, with an `AVAudioPlayer` armed against the audio
  hardware clock so a suspended app can still make a sound.
- A Live Activity for the open workout, on the lock screen and Dynamic Island, with working
  rest adjustments. Its buttons are intents running in the app process, so native owns the
  rest end-time while the activity is live and JS adopts it on resume.
- Local notifications on the device clock, replacing the server push on this target.

Everything else is the same React build. Web and UI changes need no Xcode rebuild.

## Deployment

Push to `main` runs the deploy workflow: tests, then join the server's private mesh
network, then SSH in over a key restricted to a single wrapper command. The wrapper fetches,
hard-resets to the pushed commit, rebuilds with Compose, and records the deployed SHA.

Rollback is the same workflow dispatched manually with a specific commit SHA.

Tests gate the push path only — the rollback path skips them deliberately, since requiring
`main`'s tests to pass before deploying a known-good older commit would block the fix
exactly when it's needed.
