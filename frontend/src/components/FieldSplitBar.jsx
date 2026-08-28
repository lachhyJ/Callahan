import { formatHoursMinutes } from '../utils/activityLabel'

// The 4-way on/off-field split bar + legend + honesty note, fed raw second
// totals. Shared by the single-game detail page and the tournament roll-up so
// the segment maths and the "occupancy isn't play" note copy live in one
// place. Renders nothing if there's no tracked time.
//
// onFieldContext is dropped into "...on the field <context>." — "here" for a
// single game, "across this tournament" for the weekend roll-up.
export default function FieldSplitBar({
  liveSeconds = 0,
  onFieldSeconds = 0,
  offFieldSeconds = 0,
  mixedSeconds = 0,
  onFieldContext = 'here',
}) {
  const totalTracked = onFieldSeconds + offFieldSeconds + mixedSeconds
  if (!totalTracked) return null

  const onIdleSeconds = Math.max(0, onFieldSeconds - liveSeconds)
  const pct = (part) => Math.round((part / totalTracked) * 100)
  const livePct = pct(liveSeconds)
  const onIdlePct = pct(onIdleSeconds)
  const offPct = pct(offFieldSeconds)
  const mixedPct = mixedSeconds > 0 ? pct(mixedSeconds) : null
  const liveOfOnPct = onFieldSeconds ? Math.round((liveSeconds / onFieldSeconds) * 100) : null

  return (
    <>
      <div className="field-split-bar">
        <div className="field-split-segment field-split-live" style={{ width: `${livePct}%` }} />
        <div className="field-split-segment field-split-on-idle" style={{ width: `${onIdlePct}%` }} />
        {mixedPct != null && <div className="field-split-segment field-split-mixed" style={{ width: `${mixedPct}%` }} />}
        <div className="field-split-segment field-split-off" style={{ width: `${offPct}%` }} />
      </div>
      <div className="field-split-legend">
        <span className="field-split-legend-item">
          <i className="field-timeline-swatch field-timeline-swatch-live" />
          Live play {formatHoursMinutes(liveSeconds)} · {livePct}%
        </span>
        <span className="field-split-legend-item">
          <i className="field-timeline-swatch field-timeline-swatch-on-idle" />
          On field, between points {formatHoursMinutes(onIdleSeconds)} · {onIdlePct}%
        </span>
        {mixedPct != null && (
          <span className="field-split-legend-item">
            <i className="field-timeline-swatch field-timeline-swatch-mixed" />
            Mixed {formatHoursMinutes(mixedSeconds)} · {mixedPct}%
          </span>
        )}
        <span className="field-split-legend-item">
          <i className="field-timeline-swatch field-timeline-swatch-off" />
          Off field {formatHoursMinutes(offFieldSeconds)} · {offPct}%
        </span>
      </div>
      <p className="field-split-note">
        <strong>Live play</strong> is time inside a detected point —{' '}
        {liveOfOnPct}% of your {formatHoursMinutes(onFieldSeconds)} on the field{' '}
        {onFieldContext}. The rest is waiting on the line between points, stall
        counts and subbing on. Live play misses points the detector drops, so
        read it as a slight under-count.
      </p>
    </>
  )
}
