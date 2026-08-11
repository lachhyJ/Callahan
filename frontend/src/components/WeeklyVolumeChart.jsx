const WIDTH = 160
const HEIGHT = 48
const BAR_GAP = 3

function formatVolume(v) {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(Math.round(v))
}

// Compact, non-interactive by design — this is a secondary glance-stat next
// to the calendar grid, not a primary data view (that's Muscle Balance).
export default function WeeklyVolumeChart({ weeks }) {
  const maxVolume = Math.max(...weeks.map((w) => w.volume), 1)
  const barWidth = (WIDTH - BAR_GAP * (weeks.length - 1)) / weeks.length
  const latest = weeks[weeks.length - 1]

  return (
    <div className="volume-chart">
      <div className="volume-chart-header">
        <span className="volume-chart-label">Volume / week</span>
        <span className="volume-chart-current">{formatVolume(latest.volume)} kg</span>
      </div>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="volume-chart-svg" role="img" aria-label="Weekly training volume, last 8 weeks">
        {weeks.map((w, i) => {
          const barHeight = Math.max(2, (w.volume / maxVolume) * HEIGHT)
          const x = i * (barWidth + BAR_GAP)
          const y = HEIGHT - barHeight
          const isLatest = i === weeks.length - 1
          return (
            <rect
              key={w.weekStart}
              x={x}
              y={y}
              width={barWidth}
              height={barHeight}
              rx={Math.min(2, barWidth / 2)}
              className={isLatest ? 'volume-bar current' : 'volume-bar'}
            />
          )
        })}
      </svg>
    </div>
  )
}
