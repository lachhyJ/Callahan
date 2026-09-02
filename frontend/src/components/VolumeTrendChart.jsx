import { buildTicks, niceStep } from '../utils/chartScale'
import { formatMonthShort, formatVolume } from '../utils/format'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 90
const PAD_LEFT = 34
const PAD_TOP = 8
const BAR_GAP = 8

export default function VolumeTrendChart({ months }) {
  const maxVolume = Math.max(...months.map((m) => m.volumeKg), 1)
  const step = niceStep(maxVolume)
  const yMax = Math.ceil(maxVolume / step) * step || step
  const ticks = buildTicks(0, yMax, step, 2)

  const plotWidth = WIDTH - PAD_LEFT
  const barWidth = (plotWidth - BAR_GAP * (months.length - 1)) / months.length

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Volume</h2>
      <svg viewBox={`0 0 ${WIDTH} ${PAD_TOP + HEIGHT + 16}`} className="trend-chart-svg" role="img" aria-label="Monthly training volume, in kilograms lifted">
        <ChartGridLines
          ticks={ticks}
          y={(t) => PAD_TOP + HEIGHT - (t / yMax) * HEIGHT}
          x1={PAD_LEFT}
          x2={WIDTH}
          label={formatVolume}
        />
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
                {formatMonthShort(m.monthStart)}
              </text>
            </g>
          )
        })}
      </svg>
      <span className="trend-chart-caption">kg lifted, per month</span>
    </div>
  )
}
