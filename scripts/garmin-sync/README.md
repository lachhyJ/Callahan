# Garmin sync

Pulls recent runs from Garmin Connect into Callahan via the same
`/api/activities` endpoint the app's own "Log activity" form uses. Not
CI/CD — this is a NAS cron job, same manual-deploy philosophy as the rest
of Callahan (see the repo's `docs/program-sync.md` for the general
pattern this follows).

## Setup (on the NAS)

```bash
cd /mnt/tank/callahan  # or wherever the repo's checked out
python3 -m venv scripts/garmin-sync/venv
scripts/garmin-sync/venv/bin/pip install -r scripts/garmin-sync/requirements.txt

cp scripts/garmin-sync/.env.example /mnt/tank/callahan-data/garmin-sync.env
# edit garmin-sync.env with real credentials — it's outside the repo and
# gitignored either way, same reasoning as backend.env
```

Cron entry (adjust paths):

```
0 6 * * * cd /mnt/tank/callahan && set -a && . /mnt/tank/callahan-data/garmin-sync.env && set +a && scripts/garmin-sync/venv/bin/python scripts/garmin-sync/garmin_sync.py >> /mnt/tank/callahan-data/garmin-sync.log 2>&1
```

Daily is enough — this isn't a live feed, and the 14-day lookback (see
`--days`) means a missed run or two doesn't lose anything; re-synced
activities are idempotent via `GarminActivityId`.

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
scripts/garmin-sync/venv/bin/python scripts/garmin-sync/garmin_sync.py --dry-run
```

Prints what would be synced (built Callahan payloads) without writing
anything.
