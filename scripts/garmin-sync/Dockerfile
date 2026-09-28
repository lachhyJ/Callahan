# TrueNAS SCALE's host Python has no pip/venv/ensurepip and apt is locked
# down (appliance OS) — so this runs as a container instead of a host venv,
# invoked by cron via `docker run --rm`, joined to the same compose network
# as the app so it can reach the backend by service name.
FROM python:3.12-slim
# Local time, like the backend container: the sync's date windows use
# date.today(), and on UTC a morning run in Melbourne ended its window on
# yesterday. The slim image ships no zone data, without which TZ is ignored.
RUN apt-get update && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*
ENV TZ=Australia/Melbourne
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY garmin_sync.py .
# Also baked in for the always-on `garmin-sync-trigger` compose service, which
# overrides the entrypoint to run this instead (see docker-compose.prod.yml).
COPY trigger_server.py .
ENTRYPOINT ["python", "garmin_sync.py"]
