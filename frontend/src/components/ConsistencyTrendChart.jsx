const WIDTH = 320
const HEIGHT = 90
const PAD_LEFT = 20
const PAD_TOP = 8
const GROUP_GAP = 10
const BAR_GAP = 2

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

// Session counts are small integers — a fractional gridline step (like the
// generic niceStep used for volume) would read strangely for a count metric.
function niceIntStep(max) {
  if (max <= 5) return 1
  if (max <= 10) return 2
  if (max <= 25) return 5
  return 10
}

export default function ConsistencyTrendChart({ months }) {
  const maxCount = Math.max(...months.map((m) => Math.max(m.gymSessions, m.runSessions)), 1)
  const step = niceIntStep(maxCount)
  const yMax = Math.ceil(maxCount / step) * step || step
  const ticks = []
  for (let t = 0; t <= yMax; t += step) ticks.push(t)

  const plotWidth = WIDTH - PAD_LEFT
  const groupWidth = (plotWidth - GROUP_GAP * (months.length - 1)) / months.length
  const barWidth = (groupWidth - BAR_GAP) / 2

  return (
    <div className="trend-chart">
      <div className="trend-chart-header">
        <h2 className="trend-chart-title">Consistency</h2>
        <div className="trend-chart-legend">
          <span className="trend-legend-item"><span className="trend-legend-swatch gym" />Gym</span>
          <span className="trend-legend-item"><span className="trend-legend-swatch run" />Run</span>
        </div>
      </div>
      <svg viewBox={`0 0 ${WIDTH} ${PAD_TOP + HEIGHT + 16}`} className="trend-chart-svg" role="img" aria-label="Monthly session counts, gym vs run">
        {ticks.map((t) => {
          const y = PAD_TOP + HEIGHT - (t / yMax) * HEIGHT
          return (
            <g key={t}>
              <line x1={PAD_LEFT} x2={WIDTH} y1={y} y2={y} className="chart-gridline" />
              <text x={PAD_LEFT - 4} y={y} className="chart-tick-label" textAnchor="end" dominantBaseline="middle">
                {t}
              </text>
            </g>
          )
        })}
        {months.map((m, i) => {
          const groupX = PAD_LEFT + i * (groupWidth + GROUP_GAP)
          const gymHeight = m.gymSessions > 0 ? Math.max(2, (m.gymSessions / yMax) * HEIGHT) : 0
          const runHeight = m.runSessions > 0 ? Math.max(2, (m.runSessions / yMax) * HEIGHT) : 0
          return (
            <g key={m.monthStart}>
              <rect x={groupX} y={PAD_TOP + HEIGHT - gymHeight} width={barWidth} height={gymHeight} rx="2" className="trend-bar-gym" />
              <rect x={groupX + barWidth + BAR_GAP} y={PAD_TOP + HEIGHT - runHeight} width={barWidth} height={runHeight} rx="2" className="trend-bar-run" />
              <text x={groupX + groupWidth / 2} y={PAD_TOP + HEIGHT + 12} textAnchor="middle" className="trend-chart-axis-label">
                {formatMonth(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-caption">sessions per month</span>
    </div>
  )
}
