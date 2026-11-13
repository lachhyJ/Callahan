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

const KIND_LABELS = { Point: 'point-paced', Warmup: 'long (warmup or sideline)', Other: 'rest-paced' }
const STATE_LABELS = { OnField: 'on field', OffField: 'off field', Mixed: 'mixed', Unknown: 'unknown' }

function lapTitle(lap) {
  const kind = KIND_LABELS[lap.kind] ?? lap.kind
  const gps = lap.fieldState ? ` · GPS: ${STATE_LABELS[lap.fieldState] ?? lap.fieldState}` : ''
  return `Lap ${lap.lapIndex} · ${kind} · ${formatSpan(lap.startSec, lap.endSec)}${gps}`
}

const pct = (sec, total) => `${(sec / total) * 100}%`

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
export default function FieldTimeline({ timeline }) {
  if (!timeline || timeline.segments.length === 0) return null

  const { totalSeconds, segments } = timeline
  const laps = timeline.laps ?? []
  const ticks = buildTicks(totalSeconds)

  return (
    <div className="field-timeline">
      <span className="field-timeline-label">On-field timeline</span>
      <div className="field-timeline-strip">
        {segments.map((s, i) => (
          <div
            key={i}
            className={s.onField ? 'field-timeline-band field-timeline-band-on' : 'field-timeline-band field-timeline-band-off'}
            style={{ left: pct(s.startSec, totalSeconds), width: pct(s.endSec - s.startSec, totalSeconds) }}
            title={`${s.onField ? 'On field' : 'Off field'} · ${formatSpan(s.startSec, s.endSec)}`}
          />
        ))}
        {laps.filter((l) => l.startSec > 0).map((l) => (
          <div
            key={`press-${l.lapIndex}`}
            className="field-timeline-press"
            style={{ left: pct(l.startSec, totalSeconds) }}
            title={`Lap press · ${formatClock(l.startSec)}`}
          />
        ))}
      </div>
      {laps.length > 0 && (
        <div className="field-timeline-laps" aria-label={`${laps.length} laps`}>
          {laps.map((l) => (
            <div
              key={l.lapIndex}
              className={`field-timeline-lap field-timeline-lap-${l.kind.toLowerCase()}`}
              style={{ left: pct(l.startSec, totalSeconds), width: pct(l.endSec - l.startSec, totalSeconds) }}
              title={lapTitle(l)}
            />
          ))}
        </div>
      )}
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
