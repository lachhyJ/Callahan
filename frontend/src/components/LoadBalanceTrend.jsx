import { formatDateRange } from '../dateUtils'
import { buildTicks, linearScale, niceStep, splitOnGaps } from '../utils/chartScale'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 130
const PAD_LEFT = 34
const PAD_RIGHT = 12
const PAD_TOP = 10
const PAD_BOTTOM = 22

// One load-balance bar over time: the value as a line and Garmin's target range
// as a shaded band behind it. The band is drawn per day because Garmin moves
// the range with the load. `series` is dense (buildBalanceSeries), null value =
// no sync that day. The state is also printed under the chart as a count of
// days in range, so the band is never the only channel.
export default function LoadBalanceTrend({ series, label }) {
  const real = series.map((p, i) => ({ ...p, i })).filter((p) => p.value != null)
  if (real.length < 2) return null

  const bounded = real.filter((p) => p.min != null && p.max != null)
  const yMax = Math.max(...real.map((p) => p.value), ...bounded.map((p) => p.max)) * 1.08
  const yMin = 0
  const ticks = buildTicks(yMin, yMax, niceStep(yMax - yMin), 0)

  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM
  const xFor = (i) => PAD_LEFT + (series.length <= 1 ? plotWidth / 2 : (i / (series.length - 1)) * plotWidth)
  const yFor = linearScale({ min: yMin, max: yMax, top: PAD_TOP, height: plotHeight })

  const pt = (p, key) => `${xFor(p.i).toFixed(1)} ${yFor(p[key]).toFixed(1)}`
  const linePaths = splitOnGaps(real)
    .filter((s) => s.length >= 2)
    .map((s) => s.map((p, k) => `${k === 0 ? 'M' : 'L'} ${pt(p, 'value')}`).join(' '))
  const bandPaths = splitOnGaps(bounded)
    .filter((s) => s.length >= 2)
    .map((s) => `${s.map((p, k) => `${k === 0 ? 'M' : 'L'} ${pt(p, 'max')}`).join(' ')} ${[...s]
      .reverse()
      .map((p) => `L ${pt(p, 'min')}`)
      .join(' ')} Z`)

  const inRange = bounded.filter((p) => p.value >= p.min && p.value <= p.max).length
  const first = real[0]
  const last = real[real.length - 1]
  const mid = real[Math.floor(real.length / 2)]
  const dayLabels = [
    { key: 'start', p: first, anchor: 'start' },
    ...(real.length > 6 ? [{ key: 'mid', p: mid, anchor: 'middle' }] : []),
    { key: 'end', p: last, anchor: 'end' },
  ]
  const ariaLabel =
    `${label} load, ${formatDateRange(first.date, last.date)}. ` +
    (bounded.length ? `In Garmin's target range on ${inRange} of ${bounded.length} days.` : '')

  return (
    <div className="load-balance-trend">
      <div className="progression-chart">
        <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="progression-chart-svg" role="img" aria-label={ariaLabel}>
          <ChartGridLines ticks={ticks} y={yFor} x1={PAD_LEFT} x2={WIDTH - PAD_RIGHT} />
          {bandPaths.map((d, k) => (
            <path key={`b${k}`} d={d} className="load-balance-trend-band" />
          ))}
          {linePaths.map((d, k) => (
            <path key={k} d={d} className="chart-line" />
          ))}
          {real.map((p) => (
            <circle key={p.i} cx={xFor(p.i)} cy={yFor(p.value)} r="1.4" className="chart-dot" />
          ))}
          <circle cx={xFor(last.i)} cy={yFor(last.value)} r="3" className="chart-marker" />
          {dayLabels.map((d) => (
            <text key={d.key} x={xFor(d.p.i)} y={HEIGHT - 6} className="chart-tick-label" textAnchor={d.anchor}>
              {formatDateRange(d.p.date, d.p.date)}
            </text>
          ))}
        </svg>
      </div>
      {bounded.length > 0 && (
        <p className="load-balance-trend-note">
          In Garmin&apos;s target range on {inRange} of {bounded.length} days (shaded band).
        </p>
      )}
    </div>
  )
}
