import { buildTicks, niceStep } from '../utils/chartScale'
import { formatMonthShort } from '../utils/format'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 90
const PAD_LEFT = 26
const PAD_TOP = 8
const BAR_GAP = 8

const oneDp = (v) => Number(v).toFixed(1)

// Monthly whole-recording GPS distance across Ultimate activities. One bar per
// month, the current (partial) month accented — the same shape as
// VolumeTrendChart. The per-session-type split lives in the list beneath it,
// not in stacked segments: a monthly total is the thing that reads as a load
// trend, and the app has no per-type colour vocabulary.
export default function UltimateDistanceChart({ months }) {
  const maxKm = Math.max(...months.map((m) => m.ultimateKm), 1)
  const step = niceStep(maxKm)
  const yMax = Math.ceil(maxKm / step) * step || step
  const ticks = buildTicks(0, yMax, step, 1)

  const plotWidth = WIDTH - PAD_LEFT
  const barWidth = (plotWidth - BAR_GAP * (months.length - 1)) / months.length

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Ultimate distance</h2>
      <svg
        viewBox={`0 0 ${WIDTH} ${PAD_TOP + HEIGHT + 16}`}
        className="trend-chart-svg"
        role="img"
        aria-label="Monthly distance covered across Ultimate activities, in kilometres"
      >
        <ChartGridLines
          ticks={ticks}
          y={(t) => PAD_TOP + HEIGHT - (t / yMax) * HEIGHT}
          x1={PAD_LEFT}
          x2={WIDTH}
          label={oneDp}
        />
        {months.map((m, i) => {
          const barHeight = Math.max(2, (m.ultimateKm / yMax) * HEIGHT)
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
                {formatMonthShort(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-caption">km on the field, per month</span>
    </div>
  )
}
