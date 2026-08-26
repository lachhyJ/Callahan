function formatClock(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

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

// Recomputed on every page load from the raw GPS track (see
// getActivityFieldTimeline) rather than reading a persisted table — this is
// deliberately a spot-check tool, not a source of truth, so it always shows
// what the current classifier tuning would say right now. Renders nothing
// when there's no timeline to show; the aggregate stats above it never
// depend on this succeeding.
export default function FieldTimeline({ timeline }) {
  if (!timeline || timeline.segments.length === 0) return null

  const { totalSeconds, segments } = timeline
  const ticks = buildTicks(totalSeconds)

  return (
    <div className="field-timeline">
      <span className="field-timeline-label">On-field timeline</span>
      <div className="field-timeline-strip">
        {segments.map((s, i) => (
          <div
            key={i}
            className={s.onField ? 'field-timeline-band field-timeline-band-on' : 'field-timeline-band field-timeline-band-off'}
            style={{
              left: `${(s.startSec / totalSeconds) * 100}%`,
              width: `${((s.endSec - s.startSec) / totalSeconds) * 100}%`,
            }}
            title={`${s.onField ? 'On field' : 'Off field'} · ${formatSpan(s.startSec, s.endSec)}`}
          />
        ))}
      </div>
      <div className="field-timeline-ticks">
        {ticks.map((t) => (
          <span
            key={t}
            className="field-timeline-tick"
            style={{ left: `${(t / totalSeconds) * 100}%` }}
          >
            {formatClock(t)}
          </span>
        ))}
      </div>
      <div className="field-timeline-legend">
        <span><i className="field-timeline-swatch field-timeline-swatch-on" /> On field</span>
        <span><i className="field-timeline-swatch field-timeline-swatch-off" /> Off field</span>
      </div>
    </div>
  )
}
