# ultimate-stream-explore

**Exploration scaffolding, not part of Callahan.** Answers one question: can
on-field vs sideline be recovered from an Ultimate game's GPS-speed / HR
stream when the watch was *never lap-pressed*? If yes, it's worth wiring
stream inference into the app; if no, the answer is "press the lap button".

Nothing here touches the Callahan database, the API, or the running sync.

## Flow

1. Pull raw streams with the sync script's discovery mode (runs on the NAS,
   read-only against Garmin, no Callahan writes):

   ```
   garmin_sync.py --dump-stream --start 2026-04-10 --end 2026-04-12 > tourney-stream.json
   ```

   Output is a JSON array, one object per Ultimate activity in the window,
   each with `metricDescriptors` + `activityDetailMetrics` verbatim plus
   `sampleCount` / `medianSampleSpacingSec`.

2. Analyse it here:

   ```
   python3 explore.py tourney-stream.json
   python3 explore.py --self-test      # synthetic-fixture sanity check, no input needed
   ```

   Per game: smoothed-speed Otsu threshold, on/off minutes and on-field
   stretch count (~ points played) from three segmenters (speed-only,
   HR-only, speed+HR-as-confidence), and a one-char-per-minute ASCII
   timeline to lay against memory of the game. Across the tournament:
   threshold stability and whether on-field fraction drops game 1 -> game N.

stdlib only (Python 3.11+). All tuning knobs are constants at the top of
`explore.py` - expect to fiddle them once real data is in hand.
