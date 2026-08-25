#!/usr/bin/env python3
"""Pulls recent activities from Garmin Connect into Callahan.

Run modes:
  --dump           Print the raw activityType for recent Garmin activities
                    and exit. No Callahan calls, nothing is synced. Use this
                    to find the typeKey Garmin assigns to a sport before
                    adding it to TYPE_MAP (see the Ultimate Frisbee note
                    below).
  --dump-wellness   Print raw sleep/HRV/readiness/etc payloads for one date
                    and exit. No Callahan calls, nothing is synced. Use this
                    BEFORE building any wellness sync — field names inside
                    these payloads are undocumented and watch-model support
                    varies, so the schema must be designed from real output,
                    not guessed.
  --dry-run         Fetch + map + build the Callahan payload for each
                    activity, but print instead of POSTing. Use to
                    sanity-check a mapping change before it writes anything.
  (default)         Fetch, map, and POST each mapped activity to Callahan.

Config comes entirely from environment variables (see .env.example) — this
script is meant to be invoked from a NAS cron job with a gitignored env
file, the same pattern backend.env already uses for the API itself.
"""

import argparse
import json
import os
import sys
from datetime import date, timedelta
from pathlib import Path

import garminconnect
import requests
from garminconnect import (
    Garmin,
    GarminConnectAuthenticationError,
    GarminConnectConnectionError,
    GarminConnectTooManyRequestsError,
)

# Garmin's activityType.typeKey -> Callahan's ActivityType. Only add an entry
# once you've confirmed the real typeKey with --dump — don't guess, a wrong
# guess here would silently miscategorize synced activities.
#
# "ultimate_disc" confirmed 2026-08-14 via --dump against real logged
# sessions ("Melbourne Ultimate Disc", typeId 213).
TYPE_MAP = {
    "running": "Running",
    "ultimate_disc": "Ultimate",
}

# Probed by --dump-wellness. Each is a per-date wellness endpoint on the
# garminconnect client; not every method is guaranteed to exist on whatever
# version pip resolved, and not every metric is guaranteed to be populated
# for a given watch model (training readiness in particular is limited to
# newer models) — that's exactly what the dump is for.
WELLNESS_PROBES = [
    "get_sleep_data",
    "get_hrv_data",
    "get_training_readiness",
    "get_rhr_day",
    "get_stats",
    "get_body_battery",
]

CALLAHAN_TOKEN_CACHE = Path(os.environ.get("CALLAHAN_TOKEN_CACHE", "~/.callahan_sync_token")).expanduser()
GARMIN_TOKENSTORE = Path(os.environ.get("GARMIN_TOKENSTORE", "~/.garminconnect")).expanduser()


def log(msg):
    print(msg, file=sys.stderr, flush=True)


def garmin_login():
    email = os.environ["GARMIN_EMAIL"]
    password = os.environ["GARMIN_PASSWORD"]
    client = Garmin(email=email, password=password)
    try:
        client.login(str(GARMIN_TOKENSTORE))
    except GarminConnectAuthenticationError as e:
        log(f"Garmin login failed (bad credentials or MFA required): {e}")
        raise
    except GarminConnectTooManyRequestsError as e:
        log(f"Garmin rate-limited this login: {e}")
        raise
    except GarminConnectConnectionError as e:
        log(f"Garmin connection error: {e}")
        raise
    return client


def callahan_login(api_base):
    resp = requests.post(
        f"{api_base}/api/auth/login",
        json={
            "username": os.environ["CALLAHAN_USERNAME"],
            "password": os.environ["CALLAHAN_PASSWORD"],
        },
        timeout=30,
    )
    resp.raise_for_status()
    token = resp.json()["token"]
    CALLAHAN_TOKEN_CACHE.write_text(token)
    return token


def callahan_token(api_base):
    # The token is valid 30 days (see backend/Services/TokenService.cs) — no
    # need to hit /api/auth/login every cron run, just reuse the cached one
    # until a request comes back 401.
    if CALLAHAN_TOKEN_CACHE.exists():
        return CALLAHAN_TOKEN_CACHE.read_text().strip()
    return callahan_login(api_base)


def post_activity(api_base, token, payload):
    resp = requests.post(
        f"{api_base}/api/activities",
        json=payload,
        headers={"Authorization": f"Bearer {token}"},
        timeout=30,
    )
    if resp.status_code == 401:
        # Cached token expired or was rotated server-side — log in fresh once
        # and retry, rather than failing the whole run.
        token = callahan_login(api_base)
        resp = requests.post(
            f"{api_base}/api/activities",
            json=payload,
            headers={"Authorization": f"Bearer {token}"},
            timeout=30,
        )
    resp.raise_for_status()
    return resp.json(), token


def to_int(value):
    # Garmin returns calories/heart-rate as floats (e.g. 266.0); Callahan's
    # DTO binds them as int? and rejects a JSON float with strict errors.
    return int(round(value)) if value is not None else None


def to_payload(activity, activity_type):
    summary_id = activity.get("activityId")
    distance_m = activity.get("distance")
    duration_s = activity.get("duration")
    start_local = activity.get("startTimeLocal", "")
    activity_date = start_local.split(" ")[0].split("T")[0] if start_local else None

    return {
        "date": activity_date,
        "type": activity_type,
        "durationSeconds": int(round(duration_s)) if duration_s is not None else 0,
        "distanceKm": round(distance_m / 1000, 3) if distance_m is not None else None,
        "calories": to_int(activity.get("calories")),
        "avgHeartRate": to_int(activity.get("averageHR")),
        "notes": activity.get("activityName") or None,
        "source": "Garmin",
        "garminActivityId": str(summary_id) if summary_id is not None else None,
    }


def fetch_recent_activities(client, days):
    end = date.today()
    start = end - timedelta(days=days)
    return client.get_activities_by_date(start.isoformat(), end.isoformat())


def cmd_dump(client, days):
    activities = fetch_recent_activities(client, days)
    if not activities:
        log(f"No Garmin activities in the last {days} days.")
        return
    for a in activities:
        activity_type = a.get("activityType", {})
        print(json.dumps({
            "activityId": a.get("activityId"),
            "activityName": a.get("activityName"),
            "date": a.get("startTimeLocal"),
            "typeKey": activity_type.get("typeKey"),
            "typeId": activity_type.get("typeId"),
        }, indent=2))


def cmd_dump_wellness(client, cdate):
    # Settles what the *installed* garminconnect version actually offers,
    # separately from whether Lachlan's watch reports a given metric — two
    # different failure modes that otherwise look identical from outside.
    log(f"garminconnect version: {getattr(garminconnect, '__version__', 'unknown')}")
    log(f"available get_* methods: {sorted(m for m in dir(client) if m.startswith('get_'))}")
    log(f"probing wellness for {cdate}\n")

    for name in WELLNESS_PROBES:
        method = getattr(client, name, None)
        if method is None:
            print(json.dumps({"method": name, "available": False}, indent=2))
            continue
        try:
            # get_body_battery takes a date range; every other probe here
            # takes a single cdate. Not folding this into a generic call
            # site because a wrong shared assumption would silently break
            # more probes than it saves lines.
            result = method(cdate, cdate) if name == "get_body_battery" else method(cdate)
            print(json.dumps({"method": name, "available": True, "result": result}, indent=2, default=str))
        except Exception as e:
            print(json.dumps({"method": name, "available": True, "error": f"{type(e).__name__}: {e}"}, indent=2))


def cmd_sync(client, days, dry_run, api_base):
    activities = fetch_recent_activities(client, days)
    if not activities:
        log(f"No Garmin activities in the last {days} days.")
        return

    token = None if dry_run else callahan_token(api_base)
    synced, skipped = 0, 0

    for a in activities:
        type_key = a.get("activityType", {}).get("typeKey")
        activity_type = TYPE_MAP.get(type_key)
        if activity_type is None:
            log(f"Skipping activity {a.get('activityId')} ({a.get('activityName')!r}): "
                f"unmapped typeKey '{type_key}' — run with --dump to inspect, add to TYPE_MAP once confirmed.")
            skipped += 1
            continue

        payload = to_payload(a, activity_type)
        if payload["date"] is None:
            log(f"Skipping activity {a.get('activityId')}: no startTimeLocal in response.")
            skipped += 1
            continue

        if dry_run:
            print(json.dumps(payload, indent=2))
            synced += 1
            continue

        try:
            result, token = post_activity(api_base, token, payload)
            log(f"Synced activity {payload['garminActivityId']} -> Callahan id {result.get('id')}")
            synced += 1
        except requests.HTTPError as e:
            log(f"Failed to sync activity {payload['garminActivityId']}: {e}")
            skipped += 1

    log(f"Done: {synced} synced, {skipped} skipped.")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--days", type=int, default=14,
                         help="Lookback window in days (default 14 — re-scanning tolerates missed cron runs, "
                              "and re-synced activities are idempotent via GarminActivityId).")
    parser.add_argument("--dump", action="store_true", help="Print raw activityType info and exit, no syncing.")
    parser.add_argument("--dump-wellness", action="store_true",
                         help="Print raw sleep/HRV/readiness/etc payloads for one date and exit, no syncing.")
    parser.add_argument("--wellness-date", type=str, default=None,
                         help="Date (YYYY-MM-DD) to probe with --dump-wellness. Defaults to yesterday, since "
                              "today's sleep/readiness haven't finished processing yet.")
    parser.add_argument("--dry-run", action="store_true", help="Build payloads but don't POST them.")
    args = parser.parse_args()

    api_base = os.environ.get("CALLAHAN_API_BASE", "http://localhost:8080")

    try:
        client = garmin_login()
    except (GarminConnectAuthenticationError, GarminConnectTooManyRequestsError, GarminConnectConnectionError):
        sys.exit(1)

    if args.dump:
        cmd_dump(client, args.days)
    elif args.dump_wellness:
        cdate = args.wellness_date or (date.today() - timedelta(days=1)).isoformat()
        cmd_dump_wellness(client, cdate)
    else:
        cmd_sync(client, args.days, args.dry_run, api_base)


if __name__ == "__main__":
    main()
