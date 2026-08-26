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
  --wellness        Fetch + map + PUT daily sleep/HRV/readiness/etc for a
                    date range (see --wellness-days / --wellness-start)
                    instead of syncing activities.
  --dump-laps       Print raw lap/split data for one activity (run or
                    Ultimate) and exit. No Callahan calls, nothing is synced.
                    Use this to confirm the activity has real per-lap
                    structure (a session with no lap presses returns one lap
                    for the whole thing, which makes lap classification
                    pointless for it).
  --dump-stream     Print per-second GPS/HR streams (get_activity_details)
                    for Ultimate activities in a --start/--end window, as one
                    JSON array to stdout, and exit. No Callahan calls. Feeds
                    the offline on/off-field segmentation explorer in
                    scripts/ultimate-stream-explore/ — run this first to see
                    what resolution and metric keys Garmin actually returns.
  --dump-track      Like --dump-stream, but emits the projected
                    {startEpochMs, samples:{t,lat,lon,spd}} shape the sync
                    actually PUTs to Callahan (not the raw discovery dump).
  --force-laps      During the normal sync, re-fetch and re-PUT laps (or, with
  --force-tracks    --force-tracks, the GPS track) for every in-window
                    activity even if it already has one — for after a mapping
                    or projection change. Requires --days 30 or fewer.
  --dry-run         Fetch + map + build the Callahan payload for each
                    activity (or wellness date, with --wellness), but print
                    instead of POSTing/PUTting. Use to sanity-check a
                    mapping change before it writes anything.
  (default)         Fetch, map, and POST each mapped activity to Callahan.
                    Running/Ultimate activities without laps get their laps
                    pulled and PUT to .../laps; Ultimate activities without a
                    GPS track get get_activity_details projected and PUT to
                    .../track (that's what geometric on/off-field runs on).
                    Skip with --no-laps / --no-tracks.

Config comes entirely from environment variables (see .env.example) — this
script is meant to be invoked from a NAS cron job with a gitignored env
file, the same pattern backend.env already uses for the API itself.
"""

import argparse
import json
import os
import sys
import time
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

# Callahan activity types whose laps we pull. Runs use Garmin's own per-lap
# ACTIVE labelling for high-speed distance; Ultimate "Game" activities are
# manually lap-pressed on sub on/off and Callahan classifies each lap
# on/off-field. Other Ultimate session types have no laps of interest but a
# lap PUT is harmless (the classifier only runs for Games).
LAP_SYNC_TYPES = {"Running", "Ultimate"}

# --force-laps re-fetches laps for every in-window activity, so cap the window
# it's allowed with - a stray --force-laps --days 90 would fire one Garmin
# request per activity and trip the rate limiter.
FORCE_LAPS_MAX_DAYS = 30

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


def callahan_request(method, api_base, path, token, payload):
    def send(tok):
        return requests.request(
            method, f"{api_base}{path}", json=payload,
            headers={"Authorization": f"Bearer {tok}"}, timeout=30,
        )

    resp = send(token)
    if resp.status_code == 401:
        # Cached token expired or was rotated server-side — log in fresh once
        # and retry, rather than failing the whole run.
        token = callahan_login(api_base)
        resp = send(token)
    resp.raise_for_status()
    return resp.json(), token


def post_activity(api_base, token, payload):
    return callahan_request("POST", api_base, "/api/activities", token, payload)


def put_wellness(api_base, token, payload):
    return callahan_request("PUT", api_base, "/api/wellness", token, payload)


def put_laps(api_base, token, callahan_activity_id, laps):
    return callahan_request("PUT", api_base, f"/api/activities/{callahan_activity_id}/laps", token, {"laps": laps})


def put_track(api_base, token, callahan_activity_id, track):
    return callahan_request("PUT", api_base, f"/api/activities/{callahan_activity_id}/track", token, track)


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
        # The whole summary dict verbatim - no field selection. Garmin returns
        # much more here than the columns above (training effect, activity
        # training load, max HR, elevation gain, ...); Callahan stores this as
        # a hedge so those stay recoverable without re-hitting Garmin. See
        # Activity.RawJson.
        "rawJson": json.dumps(activity, separators=(",", ":")),
    }


def fetch_laps(client, garmin_activity_id):
    result = client.get_activity_splits(garmin_activity_id)
    lap_dtos = (result or {}).get("lapDTOs") or []
    return [
        {
            "lapIndex": l.get("lapIndex"),
            "intensityType": l.get("intensityType"),
            "distanceM": l.get("distance"),
            "durationSeconds": l.get("duration"),
            "movingDurationSeconds": l.get("movingDuration"),
            "avgSpeedMps": l.get("averageSpeed"),
            "maxSpeedMps": l.get("maxSpeed"),
            "avgHeartRate": to_int(l.get("averageHR")),
            "maxHeartRate": to_int(l.get("maxHR")),
            # Absolute lap start - the join key against the GPS track for
            # geometric on/off-field labelling.
            "startTimeGmt": l.get("startTimeGMT"),
        }
        for l in lap_dtos
    ]


# Callahan-side wire/storage keys for the projected track.
_TRACK_KEYS = ("directLatitude", "directLongitude", "directSpeed", "directTimestamp")


def fetch_track(client, garmin_activity_id):
    """Pull the per-sample GPS stream and project it to Callahan's compact
    storage shape: {startEpochMs, sampleCount, medianSpacingSec,
    samples:{t,lat,lon,spd}} - t is integer seconds from startEpochMs. Returns
    None if the activity has no usable GPS stream."""
    details = client.get_activity_details(str(garmin_activity_id), maxchart=10000, maxpoly=10000)
    descriptors = details.get("metricDescriptors") or []
    rows = details.get("activityDetailMetrics") or []
    idx = {d.get("key"): d.get("metricsIndex") for d in descriptors if d.get("key") is not None}
    if not all(k in idx for k in _TRACK_KEYS) or len(rows) < 10:
        return None

    la_i, lo_i, sp_i, ts_i = (idx[k] for k in _TRACK_KEYS)
    t_raw, lat, lon, spd = [], [], [], []
    for r in rows:
        m = r.get("metrics")
        if not m or m[la_i] is None or m[lo_i] is None or m[ts_i] is None:
            continue
        t_raw.append(m[ts_i])
        lat.append(round(m[la_i], 6))
        lon.append(round(m[lo_i], 6))
        spd.append(round(m[sp_i] or 0.0, 2))
    if len(t_raw) < 10:
        return None

    t0 = int(round(t_raw[0]))
    t = [round((x - t0) / 1000) for x in t_raw]
    return {
        "startEpochMs": t0,
        "sampleCount": len(t),
        "medianSpacingSec": _stream_sample_spacing(details),
        "samples": {"t": t, "lat": lat, "lon": lon, "spd": spd},
    }


def fetch_activities_between(client, start, end):
    """start/end are datetime.date. Inclusive range."""
    return client.get_activities_by_date(start.isoformat(), end.isoformat())


def fetch_recent_activities(client, days):
    end = date.today()
    return fetch_activities_between(client, end - timedelta(days=days), end)


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


def cmd_dump_laps(client, activity_id, days):
    if activity_id is None:
        # Most recent activity of any lap-synced type in the lookback window -
        # rerun with --activity-id once --dump has surfaced the ID you want
        # (e.g. a specific game or High Speed Intervals session).
        activities = fetch_recent_activities(client, days)
        lappable = [a for a in activities
                    if TYPE_MAP.get(a.get("activityType", {}).get("typeKey")) in LAP_SYNC_TYPES]
        if not lappable:
            log(f"No lap-synced activities (runs or Ultimate) in the last {days} days. "
                f"Pass --activity-id explicitly, or use --dump to find one.")
            return
        activity_id = lappable[0]["activityId"]
        log(f"No --activity-id given, using most recent: {activity_id} ({lappable[0].get('activityName')!r})")

    for name in ("get_activity_splits", "get_activity_split_summaries"):
        method = getattr(client, name, None)
        if method is None:
            print(json.dumps({"method": name, "available": False}, indent=2))
            continue
        try:
            result = method(activity_id)
            print(json.dumps({"method": name, "available": True, "result": result}, indent=2, default=str))
        except Exception as e:
            print(json.dumps({"method": name, "available": True, "error": f"{type(e).__name__}: {e}"}, indent=2))


# Descriptor keys get_activity_details uses for the per-sample clock, in
# preference order. directTimestamp is epoch ms; sumElapsedDuration /
# sumDuration are seconds from activity start. Best-effort only - this is a
# discovery mode, so an unrecognised shape reports null spacing, never crashes.
_STREAM_TIME_KEYS = ("directTimestamp", "sumElapsedDuration", "sumDuration", "sumMovingDuration")


def _stream_sample_spacing(details):
    """Median seconds between consecutive samples, or None if undeterminable."""
    descriptors = details.get("metricDescriptors") or []
    metrics = details.get("activityDetailMetrics") or []
    if len(metrics) < 2:
        return None
    key_to_index = {d.get("key"): d.get("metricsIndex") for d in descriptors if d.get("key") is not None}
    idx = next((key_to_index[k] for k in _STREAM_TIME_KEYS if k in key_to_index), None)
    if idx is None:
        return None
    try:
        col = [row["metrics"][idx] for row in metrics if row.get("metrics") and row["metrics"][idx] is not None]
    except (IndexError, KeyError, TypeError):
        return None
    if len(col) < 2:
        return None
    # directTimestamp is ms; the sum* keys are already seconds.
    scale = 1000.0 if key_to_index.get("directTimestamp") == idx else 1.0
    diffs = sorted((col[i + 1] - col[i]) / scale for i in range(len(col) - 1))
    mid = len(diffs) // 2
    return diffs[mid] if len(diffs) % 2 else (diffs[mid - 1] + diffs[mid]) / 2


def cmd_dump_stream(client, start, end, activity_id):
    """Pull per-second GPS/HR streams for Ultimate activities in a date window
    and print them as one JSON array to stdout. No Callahan calls, nothing is
    synced. Feeds the offline segmentation explorer
    (scripts/ultimate-stream-explore/) - the point is to see what
    get_activity_details actually returns (resolution, metric keys) before any
    on/off-field inference is built on it."""
    if activity_id is not None:
        targets = [{"activityId": activity_id, "activityName": None, "startTimeLocal": None, "duration": None}]
    else:
        activities = fetch_activities_between(client, start, end)
        targets = [a for a in activities
                   if TYPE_MAP.get(a.get("activityType", {}).get("typeKey")) == "Ultimate"]
        if not targets:
            log(f"No Ultimate activities between {start} and {end}. "
                f"Check the date window, or run --dump to see what's there.")
            print("[]")
            return
        log(f"Found {len(targets)} Ultimate activit{'y' if len(targets) == 1 else 'ies'} "
            f"between {start} and {end}.")

    out = []
    for a in targets:
        aid = a["activityId"]
        name = a.get("activityName")
        try:
            # maxchart/maxpoly well above a 90-min 1 Hz stream (~5400 samples);
            # whether Garmin honours it is one of the things this dump answers.
            details = client.get_activity_details(str(aid), maxchart=10000, maxpoly=10000)
        except GarminConnectTooManyRequestsError as e:
            log(f"  Garmin rate-limited at activity {aid}: {e}. Stopping; {len(out)} dumped so far.")
            break
        except Exception as e:
            log(f"  get_activity_details failed for {aid} ({name!r}): {type(e).__name__}: {e}")
            continue

        metrics = details.get("activityDetailMetrics") or []
        spacing = _stream_sample_spacing(details)
        spacing_note = f"~{spacing:.1f}s spacing" if spacing else "spacing unknown"
        log(f"  {aid} {name!r}: {len(metrics)} samples, {spacing_note}")
        out.append({
            "activityId": aid,
            "activityName": name,
            "startTimeLocal": a.get("startTimeLocal"),
            "durationSeconds": a.get("duration"),
            "sampleCount": len(metrics),
            "medianSampleSpacingSec": spacing,
            "metricDescriptors": details.get("metricDescriptors"),
            "activityDetailMetrics": metrics,
        })

    print(json.dumps(out, default=str))


def _stream_targets(client, start, end, activity_id):
    if activity_id is not None:
        return [{"activityId": activity_id, "activityName": None}]
    activities = fetch_activities_between(client, start, end)
    targets = [a for a in activities
               if TYPE_MAP.get(a.get("activityType", {}).get("typeKey")) == "Ultimate"]
    if not targets:
        log(f"No Ultimate activities between {start} and {end}. Run --dump to see what's there.")
    return targets


def cmd_dump_track(client, start, end, activity_id):
    """Emit the projected {startEpochMs, samples:{t,lat,lon,spd}} payload the
    sync PUTs to Callahan, as one JSON array. No Callahan calls."""
    out = []
    for a in _stream_targets(client, start, end, activity_id):
        aid = a["activityId"]
        try:
            track = fetch_track(client, aid)
        except GarminConnectTooManyRequestsError as e:
            log(f"  Garmin rate-limited at activity {aid}: {e}. Stopping; {len(out)} dumped.")
            break
        except Exception as e:
            log(f"  fetch_track failed for {aid}: {type(e).__name__}: {e}")
            continue
        if track is None:
            log(f"  {aid}: no usable GPS stream")
            continue
        track["activityId"] = aid
        track["activityName"] = a.get("activityName")
        log(f"  {aid} {a.get('activityName')!r}: {track['sampleCount']} samples")
        out.append(track)
    print(json.dumps(out, default=str))


def fetch_wellness(client, cdate):
    # Trimmed to the 4 calls that actually carry new information for us —
    # get_stats already includes resting HR and body battery high/low, so
    # get_rhr_day and get_body_battery (both probed by --dump-wellness for
    # completeness) would just be two more requests for data we already have.
    raw = {}

    def probe(name):
        method = getattr(client, name, None)
        if method is None:
            return None
        try:
            result = method(cdate)
            raw[name] = result
            return result
        except GarminConnectTooManyRequestsError:
            raise
        except Exception as e:
            log(f"  {name} failed for {cdate}: {type(e).__name__}: {e}")
            return None

    sleep = probe("get_sleep_data") or {}
    daily_sleep = sleep.get("dailySleepDTO") or {}
    overall_score = (daily_sleep.get("sleepScores") or {}).get("overall") or {}

    hrv_summary = (probe("get_hrv_data") or {}).get("hrvSummary") or {}

    # Multiple readings/day (a morning baseline, then an update once
    # activity is logged) — latest by timestamp is the most complete
    # picture of the day.
    readiness_list = probe("get_training_readiness") or []
    readiness = max(readiness_list, key=lambda r: r.get("timestamp", ""), default={})

    stats = probe("get_stats") or {}

    return {
        "date": cdate,
        "sleepSeconds": daily_sleep.get("sleepTimeSeconds"),
        "deepSleepSeconds": daily_sleep.get("deepSleepSeconds"),
        "lightSleepSeconds": daily_sleep.get("lightSleepSeconds"),
        "remSleepSeconds": daily_sleep.get("remSleepSeconds"),
        "awakeSeconds": daily_sleep.get("awakeSleepSeconds"),
        "sleepScore": overall_score.get("value"),
        "sleepScoreQualifier": overall_score.get("qualifierKey"),
        "hrvLastNightAvg": hrv_summary.get("lastNightAvg"),
        "hrvWeeklyAvg": hrv_summary.get("weeklyAvg"),
        "hrvStatus": hrv_summary.get("status"),
        "trainingReadinessScore": readiness.get("score"),
        "trainingReadinessLevel": readiness.get("level"),
        "trainingReadinessFeedback": readiness.get("feedbackShort"),
        "restingHeartRate": to_int(stats.get("restingHeartRate")),
        "bodyBatteryHigh": to_int(stats.get("bodyBatteryHighestValue")),
        "bodyBatteryLow": to_int(stats.get("bodyBatteryLowestValue")),
        "avgStressLevel": to_int(stats.get("averageStressLevel")),
        "rawJson": json.dumps(raw, default=str),
    }


def wellness_date_range(days, start):
    end_d = date.today()
    start_d = date.fromisoformat(start) if start else end_d - timedelta(days=days - 1)
    d = start_d
    while d <= end_d:
        yield d
        d += timedelta(days=1)


def cmd_sync_wellness(client, days, start, dry_run, api_base):
    token = None if dry_run else callahan_token(api_base)
    synced, skipped, last_ok_date = 0, 0, None

    for d in wellness_date_range(days, start):
        cdate = d.isoformat()
        try:
            payload = fetch_wellness(client, cdate)
        except GarminConnectTooManyRequestsError as e:
            # A backfill spanning months is the realistic way to trip this —
            # stop rather than keep hammering a rate-limited account.
            log(f"Garmin rate-limited wellness fetch at {cdate}: {e}. Stopping "
                f"({'last synced date: ' + last_ok_date if last_ok_date else 'nothing synced yet'}).")
            break

        if dry_run:
            print(json.dumps(payload, indent=2))
            synced += 1
        else:
            try:
                result, token = put_wellness(api_base, token, payload)
                log(f"Synced wellness for {cdate}")
                synced += 1
                last_ok_date = cdate
            except requests.HTTPError as e:
                log(f"Failed to sync wellness for {cdate}: {e}")
                skipped += 1

        time.sleep(1.0)

    resume_from = (date.fromisoformat(last_ok_date) + timedelta(days=1)).isoformat() if last_ok_date else None
    log(f"Wellness done: {synced} synced, {skipped} skipped."
        + (f" Resume a backfill with --wellness-start {resume_from}." if start and resume_from else ""))


def cmd_sync(client, days, dry_run, api_base, sync_laps=True, force_laps=False,
             sync_tracks=True, force_tracks=False):
    activities = fetch_recent_activities(client, days)
    if not activities:
        log(f"No Garmin activities in the last {days} days.")
        return

    token = None if dry_run else callahan_token(api_base)
    synced, skipped = 0, 0
    laps_stopped = False
    tracks_stopped = False

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
            continue

        # Laps are pulled for runs and Ultimate activities. lapCount > 0 means
        # this activity's laps were already fetched on a previous run, so a
        # normal re-sync costs one extra Garmin request per *new* activity,
        # not per activity in the whole --days window. --force-laps overrides
        # that to re-fetch everything in window (e.g. after changing the lap
        # field mapping) - capped to FORCE_LAPS_MAX_DAYS, checked in main().
        if (sync_laps and not laps_stopped
                and activity_type in LAP_SYNC_TYPES
                and (force_laps or result.get("lapCount", 0) == 0)):
            try:
                laps = fetch_laps(client, a.get("activityId"))
                if laps:
                    _, token = put_laps(api_base, token, result["id"], laps)
                    log(f"  Synced {len(laps)} laps for activity {payload['garminActivityId']}")
            except GarminConnectTooManyRequestsError as e:
                log(f"  Garmin rate-limited lap fetch at activity {payload['garminActivityId']}: {e}. "
                    f"Skipping laps for the rest of this run.")
                laps_stopped = True
            except requests.HTTPError as e:
                log(f"  Failed to sync laps for activity {payload['garminActivityId']}: {e}")

        # The GPS track, for Ultimate only - it's what geometric on/off-field
        # labelling runs on. Pushed for every Ultimate activity regardless of
        # session type (the sync can't see Callahan's classification); the API
        # stores it and only classifies Games. Same once-per-new-activity
        # economy as laps via trackSampleCount; get_activity_details is a
        # heavier call than get_activity_splits, so --force-tracks is capped.
        if (sync_tracks and not tracks_stopped
                and activity_type == "Ultimate"
                and (force_tracks or result.get("trackSampleCount", 0) == 0)):
            try:
                track = fetch_track(client, a.get("activityId"))
                if track:
                    _, token = put_track(api_base, token, result["id"], track)
                    log(f"  Synced track ({track['sampleCount']} samples) for "
                        f"activity {payload['garminActivityId']}")
            except GarminConnectTooManyRequestsError as e:
                log(f"  Garmin rate-limited track fetch at activity {payload['garminActivityId']}: {e}. "
                    f"Skipping tracks for the rest of this run.")
                tracks_stopped = True
            except requests.HTTPError as e:
                log(f"  Failed to sync track for activity {payload['garminActivityId']}: {e}")

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
    parser.add_argument("--wellness", action="store_true",
                         help="Sync daily wellness (sleep/HRV/readiness/etc) instead of activities.")
    parser.add_argument("--wellness-days", type=int, default=3,
                         help="With --wellness: how many days back from today to sync (default 3 — wellness data "
                              "is final within 24-48h, so a wider window just costs more requests for no benefit; "
                              "3 still tolerates two missed cron runs).")
    parser.add_argument("--wellness-start", type=str, default=None,
                         help="With --wellness: sync every day from this date (YYYY-MM-DD) through today instead "
                              "of --wellness-days. For a one-off manual backfill — never put this on cron. "
                              "Resumable: if a run stops early (e.g. rate-limited), it logs the date to resume "
                              "from.")
    parser.add_argument("--dump-laps", action="store_true",
                         help="Print raw lap/split data for one running activity and exit, no syncing.")
    parser.add_argument("--dump-stream", action="store_true",
                         help="Print per-second GPS/HR streams for Ultimate activities in a --start/--end "
                              "window as one JSON array and exit, no syncing. Feeds the offline segmentation "
                              "explorer in scripts/ultimate-stream-explore/.")
    parser.add_argument("--start", type=str, default=None,
                         help="With --dump-stream: window start date (YYYY-MM-DD), inclusive.")
    parser.add_argument("--end", type=str, default=None,
                         help="With --dump-stream: window end date (YYYY-MM-DD), inclusive.")
    parser.add_argument("--activity-id", type=str, default=None,
                         help="With --dump-laps / --dump-stream: the Garmin activity ID to inspect (see --dump "
                              "for IDs). --dump-laps defaults to the most recent lap-synced activity in --days.")
    parser.add_argument("--dump-track", action="store_true",
                         help="Like --dump-stream but emits the projected {startEpochMs, samples:{t,lat,lon,spd}} "
                              "shape the sync PUTs to Callahan. Needs --activity-id or --start/--end.")
    parser.add_argument("--no-laps", action="store_true",
                         help="Skip lap syncing during the normal activity sync (laps are fetched by default "
                              "for any Running or Ultimate activity that doesn't have them yet).")
    parser.add_argument("--force-laps", action="store_true",
                         help=f"Re-fetch laps for every in-window activity, not just ones without laps. "
                              f"For after a lap-mapping change. Requires --days <= {FORCE_LAPS_MAX_DAYS}.")
    parser.add_argument("--no-tracks", action="store_true",
                         help="Skip GPS-track syncing (tracks are pulled by default for any Ultimate activity "
                              "that doesn't have one yet).")
    parser.add_argument("--force-tracks", action="store_true",
                         help=f"Re-fetch the GPS track for every in-window Ultimate activity, not just ones "
                              f"without one. For after a track-projection change. Requires "
                              f"--days <= {FORCE_LAPS_MAX_DAYS}.")
    parser.add_argument("--dry-run", action="store_true",
                         help="Build payloads but don't POST/PUT them (applies to --wellness too).")
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
    elif args.wellness:
        cmd_sync_wellness(client, args.wellness_days, args.wellness_start, args.dry_run, api_base)
    elif args.dump_laps:
        cmd_dump_laps(client, args.activity_id, args.days)
    elif args.dump_stream or args.dump_track:
        if args.activity_id is None and not (args.start and args.end):
            log("--dump-stream/--dump-track need either --activity-id or both --start and --end (YYYY-MM-DD).")
            sys.exit(2)
        start = date.fromisoformat(args.start) if args.start else None
        end = date.fromisoformat(args.end) if args.end else None
        if args.dump_track:
            cmd_dump_track(client, start, end, args.activity_id)
        else:
            cmd_dump_stream(client, start, end, args.activity_id)
    else:
        for flag, val in (("--force-laps", args.force_laps), ("--force-tracks", args.force_tracks)):
            if val and args.days > FORCE_LAPS_MAX_DAYS:
                log(f"{flag} refuses --days {args.days} (> {FORCE_LAPS_MAX_DAYS}): it would fetch from Garmin "
                    f"for every activity in the window and risk a rate-limit. Narrow --days.")
                sys.exit(2)
        cmd_sync(client, args.days, args.dry_run, api_base,
                 sync_laps=not args.no_laps, force_laps=args.force_laps,
                 sync_tracks=not args.no_tracks, force_tracks=args.force_tracks)


if __name__ == "__main__":
    main()
