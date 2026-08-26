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
   python3 segment.py tourney-stream.json      # THE working approach - geometry
   python3 segment.py tourney-stream.json --strict   # conservative floor on points
   ```

   Per game: on-field vs sideline minutes, points-played estimate, and a
   one-char-per-minute ASCII timeline. Validated against 6 real games
   (April 2026 tournament) and the athlete's own recollection.

## What works: geometry, not speed

Two independent structures in the GPS:

1. **Sideline vs field** - off a point he walks up and down the sideline, so he
   is *pinned to one lateral offset* while moving along the field's long axis.
   On a point he uses the full width. The feature is rolling **lateral spread**
   plus distance from the centre line. In the real data, benched stretches show
   |cross| ~20 m with only 5-10 m of lateral range; playing stretches show
   |cross| ~6-10 m with 30-47 m of range. Clean separation.
2. **Point boundaries** - between points both teams reset to opposite endzones.
   That shows as a sustained slow dwell at an extreme along-axis position. A
   dwell counts as a point he PLAYED only if the following ~90s is mostly
   on-field. Two athlete-supplied caveats drove that rule:
   - He does not always accelerate hard off an endzone (playing deep in a zone
     D, or handler on an offence start), so requiring a pull sprint/traverse
     under-counts badly - that is what `--strict` does, kept only as a floor.
   - He sometimes sits on the line for coach instructions *without* playing the
     point, then scurries back to the sideline. The follow-on on-field test
     removes those (122 raw dwells -> 101 played points).

The field frame is fitted per game from samples >=4 m/s, which are
unambiguously on-field play, so they define the long axis and the centre line.

## What does NOT work: speed thresholding (`explore.py`)

`explore.py` is the **failed** first attempt, kept as the record of why.
Smoothed-speed Otsu thresholding lands at 0.6-1.1 m/s and is unstable game to
game, because median in-game speed is only 0.5-1.0 m/s and just 16-26% of time
is above 2 m/s - Ultimate is mostly standing *even while on the field*
(stoppages, disc check-ins, setting up), so "am I moving" is not "am I on the
field". Sprint-*onset* detection fails for a related reason: a deep cut from the
stack looks identical to a pull sprint. Heart rate lags 60-120 s and disagrees
with everything. Run `explore.py --self-test` and it passes on synthetic data,
which is exactly how a wrong feature hides.

stdlib only (Python 3.11+). Tuning knobs are constants at the top of each file.
