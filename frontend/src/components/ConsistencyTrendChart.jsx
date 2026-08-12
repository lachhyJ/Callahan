const WIDTH = 320
const HEIGHT = 90
const GROUP_GAP = 10
const BAR_GAP = 2

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

export default function ConsistencyTrendChart({ months }) {
  const maxCount = Math.max(...months.map((m) => Math.max(m.gymSessions, m.runSessions)), 1)
  const groupWidth = (WIDTH - GROUP_GAP * (months.length - 1)) / months.length
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
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT + 16}`} className="trend-chart-svg" role="img" aria-label="Monthly session counts, gym vs run">
        {months.map((m, i) => {
          const groupX = i * (groupWidth + GROUP_GAP)
          const gymHeight = m.gymSessions > 0 ? Math.max(2, (m.gymSessions / maxCount) * HEIGHT) : 0
          const runHeight = m.runSessions > 0 ? Math.max(2, (m.runSessions / maxCount) * HEIGHT) : 0
          return (
            <g key={m.monthStart}>
              <rect x={groupX} y={HEIGHT - gymHeight} width={barWidth} height={gymHeight} rx="2" className="trend-bar-gym" />
              <rect x={groupX + barWidth + BAR_GAP} y={HEIGHT - runHeight} width={barWidth} height={runHeight} rx="2" className="trend-bar-run" />
              <text x={groupX + groupWidth / 2} y={HEIGHT + 12} textAnchor="middle" className="trend-chart-axis-label">
                {formatMonth(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
    </div>
  )
}
