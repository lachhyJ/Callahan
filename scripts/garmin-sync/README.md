# Garmin sync

Pulls recent runs from Garmin Connect into Callahan via the same
`/api/activities` endpoint the app's own "Log activity" form uses. Not
CI/CD — this is a NAS cron job, same manual-deploy philosophy as the rest
of Callahan (see the repo's `docs/program-sync.md` for the general
pattern this follows).

## Setup (on the NAS)

TrueNAS SCALE's host Python has no pip/venv (`apt` is locked down on the
appliance OS), so this runs as a Docker container instead of a host venv,
joined to the same compose network as the app so it can reach the
backend by service name (`http://backend:8080`) without publishing any
extra ports.

```bash
cd /mnt/tank/callahan  # or wherever the repo's checked out
sudo docker build -t callahan-garmin-sync scripts/garmin-sync/

cp scripts/garmin-sync/.env.example /mnt/tank/callahan-data/garmin-sync.env
# edit garmin-sync.env with real credentials — it's outside the repo and
# gitignored either way, same reasoning as backend.env. Set
# CALLAHAN_API_BASE=http://backend:8080 (the compose service name, not
# localhost — this runs as a separate container, not inside the backend's).

mkdir -p /mnt/tank/callahan-data/garmin-sync-state
# persists garth's Garmin session token and the cached Callahan JWT across
# cron runs (a --rm container's home dir doesn't survive between runs
# otherwise) — HOME is set to this in the docker run below.
```

## Scheduling (TrueNAS Cron Task, not a raw crontab)

TrueNAS SCALE schedules cron jobs through its own middleware (`cronjob.create`
via `midclt`, same as the existing `callahan-pdf-watch` job) rather than a
plain crontab file — `sudo crontab -e` won't show or affect it. Create via:

```bash
sudo midclt call cronjob.create '{
  "enabled": true,
  "stdout": true,
  "stderr": true,
  "schedule": {"minute": "30", "hour": "23", "dom": "*", "month": "*", "dow": "*"},
  "command": "(docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync >> /mnt/tank/callahan-data/garmin-sync.log 2>&1)",
  "description": "callahan-garmin-sync",
  "user": "root"
}'
```

Two non-obvious things baked into that command:

- **`stdout`/`stderr` must stay `true`.** TrueNAS's naming is inverted from
  what it sounds like — `true` means *suppress* (redirect to `/dev/null`),
  not capture. Worse: whenever it's *not* suppressed and the cron user has
  an email configured, TrueNAS auto-emails the full output on every run
  ("CronTask Run"). Neither behavior is separately toggleable in this API.
- **The command is wrapped in `( ... >> file 2>&1 )`, a subshell with its
  own redirect.** TrueNAS appends its own `> /dev/null 2> /dev/null` *after*
  whatever command you give it — without the subshell, that outer redirect
  overrides a plain trailing `>> file 2>&1` and silently empties it. The
  parens make our redirect apply inside, before TrueNAS's suppression wraps
  the (now-empty) outside.

The net effect: real output lands in `garmin-sync.log`, TrueNAS's own capture
sees nothing, so no job-run email. Confirmed 2026-08-15 by checking
`logs_excerpt` on a manually triggered run (`midclt call cronjob.run 5`) —
empty, while the log file had the full run output.

Daily is enough — this isn't a live feed, and the 14-day lookback (see
`--days`) means a missed run or two doesn't lose anything; re-synced
activities are idempotent via `GarminActivityId`.

Rebuild the image (`docker build` above) whenever `garmin_sync.py` or
`requirements.txt` changes — a `git pull` alone won't update the running
image.

## On-demand sync (the app's "Sync Garmin" button)

Separate from cron: `docker-compose.prod.yml` runs an always-on
`garmin-sync-trigger` service built from this same directory, with its
entrypoint overridden to `trigger_server.py` — a tiny stdlib HTTP server
(no new deps). It has **no host port**; only the backend reaches it, at
`http://garmin-sync-trigger:8099`, via `POST /api/sync/garmin` (which the
Dashboard and Games "Sync Garmin" buttons call). On each request it runs
the same `cmd_sync` (+ `cmd_sync_wellness` when `?wellness=1`) this script's
default mode does, one at a time (a second concurrent request gets `409`).

It reads the **same** `/mnt/tank/callahan-data/garmin-sync.env` the cron job
uses. Two optional knobs there:

- `TRIGGER_TOKEN` — shared secret. If set, the backend must send the same
  value as `Sync__TriggerToken` in `backend.prod.env`. If unset, the trigger
  accepts any request from the compose network (fine for this single-user
  setup behind Cloudflare).
- `TRIGGER_SYNC_DAYS` / `TRIGGER_WELLNESS_DAYS` — lookback windows
  (default 14 / 3).

Enable it by setting `Sync__TriggerBaseUrl=http://garmin-sync-trigger:8099`
in `backend.prod.env` (see `backend.prod.env.example`); leave it blank and
the button 502s with a clear message, everything else unaffected. The
nightly cron `docker run --rm` is untouched — the button is additive and
every write is idempotent, so the two can even overlap harmlessly.

`deploy.sh`'s `docker compose ... up -d --build` builds and (re)starts this
service on every deploy — no manual `docker build` step for it, unlike the
cron image.

## Mapped activity types

`TYPE_MAP` in `garmin_sync.py` currently covers:

- `running` → Running
- `ultimate_disc` → Ultimate (confirmed 2026-08-14 via `--dump` against
  real logged "Melbourne Ultimate Disc" sessions)

Everything else Garmin reports (snowboarding, strength training, hiking,
...) is deliberately unmapped — those activities are logged and skipped,
never guessed at or defaulted to an existing type. To add a new sport,
run `--dump` after logging a real session of that type on Garmin, find
its `typeKey` in the output, and only add it to `TYPE_MAP` once you've
confirmed it against real data — not by guessing from Garmin's UI label.

## Wellness discovery (`--dump-wellness`)

Before any wellness sync gets built, run this to see what Garmin's
sleep/HRV/readiness/etc endpoints actually return for Lachlan's specific
watch — field names inside these payloads are undocumented and
version-dependent, and training readiness only exists on newer watch
models, so the schema has to come from real output, not a guess.

```bash
sudo docker build -t callahan-garmin-sync scripts/garmin-sync/
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync --dump-wellness
```

No Callahan calls, nothing is synced. Defaults to yesterday's date (today's
sleep/readiness haven't finished Garmin's overnight processing); pass
`--wellness-date YYYY-MM-DD` to probe a specific day. Run it for at least
two different dates — a normal night and one after a hard session — so you
can tell which fields actually vary from which are constant-null for this
watch. Each probe call is independent and wrapped in its own try/except, so
one missing method or 404 doesn't blank the rest of the dump.

**Confirmed 2026-08-25** against Lachlan's watch (two dates): sleep
(duration + deep/light/rem/awake splits + overall score/qualifier), HRV
(last-night avg, weekly avg, status), and training readiness (score, level,
feedback) are all populated — nothing came back permanently null. Training
readiness returns *multiple* readings per day (a morning baseline, then an
update once activity is logged); the sync takes the latest by timestamp.
`get_stats` alone already carries resting heart rate and body-battery
high/low, making `get_rhr_day` and `get_body_battery` redundant for our
purposes — the sync only calls `get_sleep_data`, `get_hrv_data`,
`get_training_readiness`, and `get_stats` (4 requests/date, not the 6
`--dump-wellness` probes). Also confirmed: the `calendarDate` Garmin
returns matches the date queried (the wake-up morning), so `DailyWellness.Date`
keys consistently off the date passed to the API with no off-by-one risk.

## Stream discovery (`--dump-stream`)

Pulls the per-second GPS-speed / heart-rate stream (`get_activity_details`)
for every Ultimate activity in a date window and prints it as one JSON array
to stdout — the raw material for the offline on/off-field segmentation
explorer in `scripts/ultimate-stream-explore/`. No Callahan calls, nothing
is synced.

```bash
sudo docker build -t callahan-garmin-sync scripts/garmin-sync/   # only if the script changed
sudo docker run --rm --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data \
  -v /mnt/tank/callahan-data/garmin-sync-state:/data \
  callahan-garmin-sync --dump-stream --start 2026-04-10 --end 2026-04-12 > tourney-stream.json
```

`--start` / `--end` (both `YYYY-MM-DD`, inclusive) are required, or
`--activity-id` for a single one. Per-activity progress — sample count and
median spacing — goes to stderr; the JSON payload is the only thing on
stdout, so redirecting it to a file is clean. The `--network` flag isn't
needed here (this mode never calls the Callahan API). Each object carries
`metricDescriptors` + `activityDetailMetrics` verbatim plus a best-effort
`sampleCount` / `medianSampleSpacingSec`, because whether Garmin honours the
high `maxchart`/`maxpoly` request is one of the things this dump answers.

`--dump-track` is the same selection but emits the *projected*
`{startEpochMs, sampleCount, medianSpacingSec, samples:{t,lat,lon,spd}}`
shape — exactly what the normal sync PUTs to `/api/activities/{id}/track`.

## GPS tracks in the normal sync

The default run also pushes the projected GPS track for any **Ultimate**
activity that doesn't have one yet (`trackSampleCount == 0` in the POST
response), to `PUT /api/activities/{id}/track`. That's the data geometric
on/off-field labelling runs on — the API stores it for every Ultimate
activity and only classifies the ones marked "Game". `--no-tracks` skips
it; `--force-tracks` re-pulls every in-window Ultimate track (capped to
`--days 30`, same as `--force-laps`) — use it after changing the projection
in `fetch_track`. `get_activity_details` is heavier than the lap call, so a
tournament weekend is fine but don't `--force-tracks` a wide window.

### Backfilling a past date range

The normal sync counts back `--days` from today. To pull a fixed historical
window instead — e.g. a past tournament — pass `--start`/`--end`
(`YYYY-MM-DD`, inclusive, both required). New activities in the window get
their laps and (for Ultimate) tracks fetched automatically; `--force-*`
isn't needed for a first backfill. Keep each run to one weekend so a
`get_activity_details`-per-game rate-limit stops cheaply (the run is
resumable — just re-run it).

```bash
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env \
  -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data \
  callahan-garmin-sync --start 2026-04-10 --end 2026-04-12
```

The activities land unclassified; set each to session type "Game" in the
app (or `POST /api/activities/laps/reclassify?force=true` once they're all
classified) to compute the on/off-field split.

## Wellness sync (`--wellness`)

```bash
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync --wellness
```

Syncs the last `--wellness-days` days (default 3, ending today) to
`PUT /api/wellness`, upserting by date. 3 days is enough to tolerate two
missed cron runs; wellness data is final within 24-48h so a wider default
window would just cost more Garmin requests for no benefit. Add `--dry-run`
to print payloads without writing. Rate-limit safe: 1s pause between dates,
aborts the run (rather than retrying) on a 429.

**Scheduling:** two TrueNAS cron entries, both running `--wellness` —
noon (after Garmin's overnight sleep/HRV/readiness processing has landed)
and appended to the existing 23:30 activities run (a second chance if the
watch synced late or the noon run failed). Both are safely idempotent, so
overlapping runs just rewrite the same rows. Create the noon entry the same
way as the existing job (see Scheduling above), with `"hour": "12"` and its
own log file (e.g. `garmin-sync-wellness.log`); for the 23:30 entry, append
` --wellness` to the existing job's `command` (either edit it in place via
`midclt call cronjob.update <id> '{...}'` or delete + recreate) — running
both activities and wellness in the same invocation, rather than as two
separate containers, avoids a third TrueNAS cron entry for something that
already happens on the same schedule.

**Backfill:**

```bash
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync --wellness --wellness-start 2026-07-26
```

One-off, manual, watched — **never on cron**. At 4 requests/date + 1s
sleep, 30 days is a couple of minutes; Lachlan's stated intent is to come
back for 6+ months of history later, so start with a smaller window first
to confirm the rate limiter tolerates it and the field mapping holds
against older data before going wider. If Garmin rate-limits mid-run, the
script stops and logs the exact `--wellness-start` date to resume from,
rather than restarting the whole backfill from scratch.

## First run / sanity check

```bash
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync --dry-run
```

Prints what would be synced (built Callahan payloads) without writing
anything.
