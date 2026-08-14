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

Cron entry (adjust paths; find the compose network name with
`docker network ls | grep callahan` if it's not `callahan_default`):

```
0 6 * * * sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync >> /mnt/tank/callahan-data/garmin-sync.log 2>&1
```

Daily is enough — this isn't a live feed, and the 14-day lookback (see
`--days`) means a missed run or two doesn't lose anything; re-synced
activities are idempotent via `GarminActivityId`.

Rebuild the image (`docker build` above) whenever `garmin_sync.py` or
`requirements.txt` changes — a `git pull` alone won't update the running
image.

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

## First run / sanity check

```bash
sudo docker run --rm --network callahan_default --env-file /mnt/tank/callahan-data/garmin-sync.env -e HOME=/data -v /mnt/tank/callahan-data/garmin-sync-state:/data callahan-garmin-sync --dry-run
```

Prints what would be synced (built Callahan payloads) without writing
anything.
