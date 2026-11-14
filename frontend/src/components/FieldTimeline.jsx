import { useState } from 'react'
import { formatClock } from '../utils/format'

function formatSpan(startSec, endSec) {
  const minutes = Math.round((endSec - startSec) / 60)
  return `${formatClock(startSec)}–${formatClock(endSec)} (${minutes} min)`
}

// A tick every 10 minutes, skipping any that would crowd the final label.
function buildTicks(totalSeconds) {
  const stepSec = 600
  const ticks = []
  for (let t = 0; t <= totalSeconds; t += stepSec) ticks.push(t)
  return ticks
}

const KIND_LABELS = { Point: 'point pace', Warmup: 'long lap', Other: 'rest pace' }
const STATE_LABELS = { OnField: 'on field', OffField: 'off field', Mixed: 'mixed', Unknown: 'unknown' }

const pct = (sec, total) => `${(sec / total) * 100}%`

// The span containing second t: the last one starting at or before it.
function spanAt(spans, t) {
  let hit = null
  for (const s of spans) if (s.startSec <= t) hit = s
  return hit
}

// Recomputed on every page load from the raw GPS track (see
// getActivityFieldTimeline) rather than reading a persisted table — this is
// deliberately a spot-check tool, not a source of truth, so it always shows
// what the current classifier tuning would say right now. Renders nothing
// when there's no timeline to show; the aggregate stats above it never
// depend on this succeeding.
//
// When the game was lap-pressed, each press is drawn as a line across the
// strip, and a thin row underneath colours each lap by its own pace. The
// strip is the GPS geometry's call, the row is the watch's — where they
// disagree is where to look.
//
// Tapping anywhere on the strip or the lap row picks that moment and spells
// out the stint and lap under it. The whole area is the target, not each
// band: a short lap is a few pixels wide on a phone, and title tooltips
// never show on touch.
export default function FieldTimeline({ timeline }) {
  const [pickedSec, setPickedSec] = useState(null)
  if (!timeline || timeline.segments.length === 0) return null

  const { totalSeconds, segments } = timeline
  const laps = timeline.laps ?? []
  const ticks = buildTicks(totalSeconds)

  function pick(e) {
    const r = e.currentTarget.getBoundingClientRect()
    const f = Math.min(1, Math.max(0, (e.clientX - r.left) / r.width))
    setPickedSec(Math.round(f * totalSeconds))
  }

  const seg = pickedSec == null ? null : spanAt(segments, pickedSec)
  const lap = pickedSec == null ? null : spanAt(laps, pickedSec)

  return (
    <div className="field-timeline">
      <span className="field-timeline-label">On-field timeline</span>
      <div className="field-timeline-pick" onClick={pick}>
        <div className="field-timeline-strip">
          {segments.map((s, i) => (
            <div
              key={i}
              className={s.onField ? 'field-timeline-band field-timeline-band-on' : 'field-timeline-band field-timeline-band-off'}
              style={{ left: pct(s.startSec, totalSeconds), width: pct(s.endSec - s.startSec, totalSeconds) }}
            />
          ))}
          {laps.filter((l) => l.startSec > 0).map((l) => (
            <div
              key={`press-${l.lapIndex}`}
              className="field-timeline-press"
              style={{ left: pct(l.startSec, totalSeconds) }}
            />
          ))}
        </div>
        {laps.length > 0 && (
          <div className="field-timeline-laps">
            {laps.map((l) => (
              <div
                key={l.lapIndex}
                className={`field-timeline-lap field-timeline-lap-${l.kind.toLowerCase()}${lap === l ? ' field-timeline-lap-picked' : ''}`}
                style={{ left: pct(l.startSec, totalSeconds), width: pct(l.endSec - l.startSec, totalSeconds) }}
              />
            ))}
          </div>
        )}
        {pickedSec != null && (
          <div className="field-timeline-cursor" style={{ left: pct(pickedSec, totalSeconds) }} />
        )}
      </div>
      <div className="field-timeline-ticks">
        {ticks.map((t) => (
          <span
            key={t}
            className="field-timeline-tick"
            style={{ left: pct(t, totalSeconds) }}
          >
            {formatClock(t)}
          </span>
        ))}
      </div>
      <p className="field-timeline-detail" aria-live="polite">
        {pickedSec == null
          ? 'Tap the timeline for details.'
          : (
            <>
              <strong>{formatClock(pickedSec)}</strong>
              {seg && ` · GPS: ${seg.onField ? 'on field' : 'off field'} ${formatSpan(seg.startSec, seg.endSec)}`}
              {lap && (
                <>
                  <br />
                  {`Lap ${lap.lapIndex} · ${KIND_LABELS[lap.kind] ?? lap.kind} · ${formatSpan(lap.startSec, lap.endSec)}`}
                  {lap.fieldState && ` · GPS called it ${STATE_LABELS[lap.fieldState] ?? lap.fieldState}`}
                </>
              )}
            </>
          )}
      </p>
      <div className="field-timeline-legend">
        <span><i className="field-timeline-swatch field-timeline-swatch-on" /> On field</span>
        <span><i className="field-timeline-swatch field-timeline-swatch-off" /> Off field</span>
        {laps.length > 0 && (
          <>
            <span><i className="field-timeline-swatch field-timeline-swatch-press" /> Lap press</span>
            <span><i className="field-timeline-swatch field-timeline-swatch-point" /> Point-paced lap</span>
          </>
        )}
      </div>
    </div>
  )
}
