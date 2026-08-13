const WIDTH = 320
const HEIGHT = 90
const PAD_LEFT = 34
const PAD_TOP = 8
const BAR_GAP = 8

function formatVolume(v) {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(Math.round(v))
}

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

function niceStep(range) {
  const rough = range / 3
  const magnitude = 10 ** Math.floor(Math.log10(rough || 1))
  const normalized = rough / magnitude
  const step = normalized < 1.5 ? 1 : normalized < 3.5 ? 2.5 : normalized < 7.5 ? 5 : 10
  return step * magnitude
}

export default function VolumeTrendChart({ months }) {
  const maxVolume = Math.max(...months.map((m) => m.volumeKg), 1)
  const step = niceStep(maxVolume)
  const yMax = Math.ceil(maxVolume / step) * step || step
  const ticks = []
  for (let t = 0; t <= yMax; t += step) ticks.push(t)

  const plotWidth = WIDTH - PAD_LEFT
  const barWidth = (plotWidth - BAR_GAP * (months.length - 1)) / months.length

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Volume</h2>
      <svg viewBox={`0 0 ${WIDTH} ${PAD_TOP + HEIGHT + 16}`} className="trend-chart-svg" role="img" aria-label="Monthly training volume, in kilograms lifted">
        {ticks.map((t) => {
          const y = PAD_TOP + HEIGHT - (t / yMax) * HEIGHT
          return (
            <g key={t}>
              <line x1={PAD_LEFT} x2={WIDTH} y1={y} y2={y} className="chart-gridline" />
              <text x={PAD_LEFT - 6} y={y} className="chart-tick-label" textAnchor="end" dominantBaseline="middle">
                {formatVolume(t)}
              </text>
            </g>
          )
        })}
        {months.map((m, i) => {
          const barHeight = Math.max(2, (m.volumeKg / yMax) * HEIGHT)
          const x = PAD_LEFT + i * (barWidth + BAR_GAP)
          const isLast = i === months.length - 1
          return (
            <g key={m.monthStart}>
              <rect
                x={x}
                y={PAD_TOP + HEIGHT - barHeight}
                width={barWidth}
                height={barHeight}
                rx={Math.min(3, barWidth / 2)}
                className={isLast ? 'trend-bar current' : 'trend-bar'}
              />
              <text x={x + barWidth / 2} y={PAD_TOP + HEIGHT + 12} textAnchor="middle" className="trend-chart-axis-label">
                {formatMonth(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-caption">kg lifted, per month</span>
    </div>
  )
}
