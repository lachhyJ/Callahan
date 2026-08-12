const WIDTH = 320
const HEIGHT = 90
const BAR_GAP = 8

function formatVolume(v) {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(Math.round(v))
}

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

export default function VolumeTrendChart({ months }) {
  const maxVolume = Math.max(...months.map((m) => m.volumeKg), 1)
  const barWidth = (WIDTH - BAR_GAP * (months.length - 1)) / months.length

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Volume</h2>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT + 16}`} className="trend-chart-svg" role="img" aria-label="Monthly training volume">
        {months.map((m, i) => {
          const barHeight = Math.max(2, (m.volumeKg / maxVolume) * HEIGHT)
          const x = i * (barWidth + BAR_GAP)
          const isLast = i === months.length - 1
          return (
            <g key={m.monthStart}>
              <rect
                x={x}
                y={HEIGHT - barHeight}
                width={barWidth}
                height={barHeight}
                rx={Math.min(3, barWidth / 2)}
                className={isLast ? 'trend-bar current' : 'trend-bar'}
              />
              <text x={x + barWidth / 2} y={HEIGHT + 12} textAnchor="middle" className="trend-chart-axis-label">
                {formatMonth(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-current">{formatVolume(months[months.length - 1].volumeKg)} kg this month</span>
    </div>
  )
}
