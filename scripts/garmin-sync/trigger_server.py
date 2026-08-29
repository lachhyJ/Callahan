#!/usr/bin/env python3
"""Always-on HTTP trigger for an on-demand Garmin sync.

Runs as its own long-lived container (see docker-compose.prod.yml's
`garmin-sync-trigger` service), on the compose network, with no host port.
The backend's `POST /api/sync/garmin` is the only thing that calls it; that
in turn is the "Sync Garmin" button in the app.

It runs the *same* `garmin_sync` module the nightly TrueNAS cron job does —
`cmd_sync` (activities/laps/tracks) and optionally `cmd_sync_wellness`. The
cron `docker run --rm` is left exactly as-is: this is purely additive, and
every write the sync makes is idempotent (activities keyed on
GarminActivityId, wellness/laps/track upserted), so an overlap with the
nightly run is harmless.

Config (all via env, shared with the cron job's env file):
  CALLAHAN_API_BASE   backend base URL (compose: http://backend:8080)
  GARMIN_EMAIL / GARMIN_PASSWORD / CALLAHAN_USERNAME / CALLAHAN_PASSWORD
  TRIGGER_TOKEN       optional shared secret; when set, POST /sync must send
                      a matching X-Sync-Token header
  TRIGGER_PORT        listen port (default 8099)
  TRIGGER_SYNC_DAYS / TRIGGER_WELLNESS_DAYS   lookback windows (defaults 14 / 3)
"""

import io
import json
import os
import threading
import time
from contextlib import redirect_stderr
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import garmin_sync

PORT = int(os.environ.get("TRIGGER_PORT", "8099"))
TOKEN = os.environ.get("TRIGGER_TOKEN") or None
API_BASE = os.environ.get("CALLAHAN_API_BASE", "http://backend:8080")
SYNC_DAYS = int(os.environ.get("TRIGGER_SYNC_DAYS", "14"))
WELLNESS_DAYS = int(os.environ.get("TRIGGER_WELLNESS_DAYS", "3"))

# One sync at a time. A second request while one is in flight gets 409 rather
# than queueing or running Garmin calls concurrently (which would risk the
# rate limiter).
_lock = threading.Lock()


def _run_sync(wellness):
    """Run the sync in-process, capturing the module's stderr log lines.
    Returns (ok, summary_dict)."""
    buf = io.StringIO()
    started = time.monotonic()
    ok, err = True, None
    try:
        with redirect_stderr(buf):
            client = garmin_sync.garmin_login()
            garmin_sync.cmd_sync(client, SYNC_DAYS, False, API_BASE)
            if wellness:
                garmin_sync.cmd_sync_wellness(client, WELLNESS_DAYS, None, False, API_BASE)
    except Exception as e:  # report any failure back to the caller, don't crash the server
        ok, err = False, f"{type(e).__name__}: {e}"

    lines = [ln for ln in buf.getvalue().splitlines() if ln.strip()]
    return ok, {
        "ok": ok,
        "wellness": wellness,
        "durationMs": round((time.monotonic() - started) * 1000),
        "error": err,
        "log": lines[-25:],
    }


class Handler(BaseHTTPRequestHandler):
    server_version = "GarminSyncTrigger/1.0"

    def _send(self, code, body):
        payload = json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        if self.path == "/health":
            self._send(200, {"ok": True, "busy": _lock.locked()})
        else:
            self._send(404, {"error": "not found"})

    def do_POST(self):
        if self.path.split("?", 1)[0] != "/sync":
            self._send(404, {"error": "not found"})
            return
        if TOKEN and self.headers.get("X-Sync-Token") != TOKEN:
            self._send(401, {"error": "bad or missing X-Sync-Token"})
            return

        # Drain any request body so the connection closes cleanly; params come
        # from the query string.
        body_len = int(self.headers.get("Content-Length") or 0)
        if body_len:
            self.rfile.read(body_len)
        query = self.path.split("?", 1)[1] if "?" in self.path else ""
        wellness = "wellness=1" in query or "wellness=true" in query

        if not _lock.acquire(blocking=False):
            self._send(409, {"error": "a sync is already running"})
            return
        try:
            ok, summary = _run_sync(wellness)
        finally:
            _lock.release()
        self._send(200 if ok else 502, summary)

    def log_message(self, fmt, *args):
        print("trigger: " + (fmt % args), flush=True)


def main():
    server = ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
    print(
        f"garmin-sync trigger listening on :{PORT} "
        f"(token {'required' if TOKEN else 'disabled'}, api {API_BASE})",
        flush=True,
    )
    server.serve_forever()


if __name__ == "__main__":
    main()
