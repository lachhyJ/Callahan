import { formatHoursMinutes } from '../utils/activityLabel'

// A two-segment split: live play vs everything else (waiting on the line
// between points, subbing on, mixed lap-press time, off the field). The
// between-points band used to be its own segment but it didn't earn the
// visual weight - the honest, useful cut is live / not-live. Shared by the
// single-game detail page and the tournament roll-up. Renders nothing if
// there's no tracked time.
export default function FieldSplitBar({
  liveSeconds = 0,
  onFieldSeconds = 0,
  offFieldSeconds = 0,
  mixedSeconds = 0,
}) {
  const totalTracked = onFieldSeconds + offFieldSeconds + mixedSeconds
  if (!totalTracked) return null

  const live = Math.max(0, Math.min(liveSeconds, totalTracked))
  const notLive = totalTracked - live
  const pct = (part) => Math.round((part / totalTracked) * 100)
  const livePct = pct(live)

  return (
    <>
      <div className="field-split-bar">
        <div className="field-split-segment field-split-live" style={{ width: `${livePct}%` }} />
        <div className="field-split-segment field-split-off" style={{ width: `${100 - livePct}%` }} />
      </div>
      <div className="field-split-legend">
        <span className="field-split-legend-item">
          <i className="field-timeline-swatch field-timeline-swatch-live" />
          Live play {formatHoursMinutes(live)} · {livePct}%
        </span>
        <span className="field-split-legend-item">
          <i className="field-timeline-swatch field-timeline-swatch-off" />
          Not live {formatHoursMinutes(notLive)} · {100 - livePct}%
        </span>
      </div>
      <p className="field-split-note">
        <strong>Live play</strong> is time inside a detected point. Everything
        else — waiting on the line between points, stall counts, subbing on, or
        off the field — is grouped as not live. Live play misses points the
        detector drops, so read it as a slight under-count.
      </p>
    </>
  )
}
