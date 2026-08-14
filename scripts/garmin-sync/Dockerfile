# TrueNAS SCALE's host Python has no pip/venv/ensurepip and apt is locked
# down (appliance OS) — so this runs as a container instead of a host venv,
# invoked by cron via `docker run --rm`, joined to the same compose network
# as the app so it can reach the backend by service name.
FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY garmin_sync.py .
ENTRYPOINT ["python", "garmin_sync.py"]
