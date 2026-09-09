import { buildTicks, niceStep } from '../utils/chartScale'
import { formatMonthShort, formatVolume } from '../utils/format'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 90
const PAD_LEFT = 30
const PAD_TOP = 8
const BAR_GAP = 8

// Garmin's summed activity training load per month, the Ultimate share stacked
// at the base. A month with no scored session (totalTrainingLoad null) draws
// no bar rather than a zero. Same bar geometry as VolumeTrendChart.
export default function TrainingLoadChart({ months }) {
  const maxLoad = Math.max(...months.map((m) => m.totalTrainingLoad ?? 0), 1)
  const step = niceStep(maxLoad)
  const yMax = Math.ceil(maxLoad / step) * step || step
  const ticks = buildTicks(0, yMax, step, 0)

  const plotWidth = WIDTH - PAD_LEFT
  const barWidth = (plotWidth - BAR_GAP * (months.length - 1)) / months.length
  const yOf = (v) => PAD_TOP + HEIGHT - (v / yMax) * HEIGHT

  return (
    <div className="trend-chart">
      <div className="trend-chart-header">
        <h2 className="trend-chart-title">Training load</h2>
        <div className="trend-chart-legend">
          <span className="trend-legend-item"><span className="trend-legend-swatch gym" />Ultimate</span>
          <span className="trend-legend-item"><span className="trend-legend-swatch muted" />Other</span>
        </div>
      </div>
      <svg
        viewBox={`0 0 ${WIDTH} ${PAD_TOP + HEIGHT + 16}`}
        className="trend-chart-svg"
        role="img"
        aria-label="Monthly Garmin training load, Ultimate share stacked at the base"
      >
        <ChartGridLines ticks={ticks} y={yOf} x1={PAD_LEFT} x2={WIDTH} label={formatVolume} />
        {months.map((m, i) => {
          const x = PAD_LEFT + i * (barWidth + BAR_GAP)
          const label = (
            <text key={`x-${m.monthStart}`} x={x + barWidth / 2} y={PAD_TOP + HEIGHT + 12} textAnchor="middle" className="trend-chart-axis-label">
              {formatMonthShort(m.monthStart)}
            </text>
          )
          if (m.totalTrainingLoad == null) return label

          const ultimate = m.ultimateTrainingLoad ?? 0
          const other = Math.max(0, m.totalTrainingLoad - ultimate)
          const rx = Math.min(3, barWidth / 2)
          return (
            <g key={m.monthStart}>
              {other > 0 && (
                <rect x={x} y={yOf(m.totalTrainingLoad)} width={barWidth} height={Math.max(1, yOf(ultimate) - yOf(m.totalTrainingLoad))} rx={rx} className="trend-bar" />
              )}
              {ultimate > 0 && (
                <rect x={x} y={yOf(ultimate)} width={barWidth} height={Math.max(1, PAD_TOP + HEIGHT - yOf(ultimate))} rx={rx} className="trend-bar-gym" />
              )}
              {label}
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-caption">Garmin activity load, summed per month</span>
    </div>
  )
}
