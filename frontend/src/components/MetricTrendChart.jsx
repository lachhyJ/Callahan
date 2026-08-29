import { niceStep } from '../utils/chartScale'

const WIDTH = 320
const HEIGHT = 110
const PAD_LEFT = 34
const PAD_RIGHT = 12
const PAD_TOP = 12
const PAD_BOTTOM = 22

function formatDay(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('en-AU', { day: 'numeric', month: 'short' })
}

// One wellness metric over ~12 weeks of daily readings: line + area + a dashed
// rule at the 28-day baseline. Non-interactive (v1 — a Daily/Weekly toggle and
// a crosshair are deferred). Modelled on TournamentGameChart but with a padded,
// non-zero Y domain (readiness / HRV / resting HR sit in narrow high bands) and
// gap-aware paths, since Garmin skips days. `points` values are already in
// display units; a null value marks a missing day. The caption lives once at
// the top of the list (WellnessPage), not per-chart — this just takes an
// `ariaLabel` for the SVG.
export default function MetricTrendChart({ points, baselineAvg, ariaLabel }) {
  const real = points.map((p, i) => ({ ...p, i })).filter((p) => p.value != null)
  if (real.length < 2) return null

  const nums = real.map((p) => p.value)
  const domainRefs = baselineAvg != null ? [...nums, baselineAvg] : nums
  const rawMin = Math.min(...domainRefs)
  const rawMax = Math.max(...domainRefs)
  const pad = Math.max(1, (rawMax - rawMin) * 0.15 || rawMax * 0.1 || 1)
  const yMin = Math.max(0, rawMin - pad)
  const yMax = rawMax + pad

  const step = niceStep(yMax - yMin)
  const ticks = []
  for (let t = Math.ceil(yMin / step) * step; t <= yMax + 1e-9; t += step) {
    ticks.push(Math.round(t * 10) / 10)
  }

  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM

  const xFor = (i) => PAD_LEFT + (points.length <= 1 ? plotWidth / 2 : (i / (points.length - 1)) * plotWidth)
  const yFor = (v) => PAD_TOP + plotHeight - ((v - yMin) / (yMax - yMin)) * plotHeight

  // Split into segments so a run of missing days lifts the pen.
  const segments = []
  let cur = []
  let prevI = null
  for (const p of real) {
    if (prevI != null && p.i !== prevI + 1) {
      segments.push(cur)
      cur = []
    }
    cur.push(p)
    prevI = p.i
  }
  if (cur.length) segments.push(cur)

  const drawable = segments.filter((s) => s.length >= 2)
  const linePaths = drawable.map((s) =>
    s.map((p, k) => `${k === 0 ? 'M' : 'L'} ${xFor(p.i).toFixed(1)} ${yFor(p.value).toFixed(1)}`).join(' ')
  )
  // Faint dashed connectors span the days Garmin skipped, so a run of gaps
  // reads as one trend rather than a row of disconnected blocks. Every real
  // reading (including a lone one between two gaps) also gets a dot.
  const bridgePaths = []
  for (let k = 1; k < real.length; k++) {
    if (real[k].i !== real[k - 1].i + 1) {
      bridgePaths.push(
        `M ${xFor(real[k - 1].i).toFixed(1)} ${yFor(real[k - 1].value).toFixed(1)} ` +
          `L ${xFor(real[k].i).toFixed(1)} ${yFor(real[k].value).toFixed(1)}`
      )
    }
  }

  const first = real[0]
  const mid = real[Math.floor(real.length / 2)]
  const last = real[real.length - 1]
  const dayLabels = [
    { key: 'start', i: first.i, date: first.date, anchor: 'start' },
    ...(real.length > 6 ? [{ key: 'mid', i: mid.i, date: mid.date, anchor: 'middle' }] : []),
    { key: 'end', i: last.i, date: last.date, anchor: 'end' },
  ]

  const showBaseline = baselineAvg != null && baselineAvg >= yMin && baselineAvg <= yMax

  return (
    <div className="progression-chart">
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="progression-chart-svg" role="img" aria-label={ariaLabel}>
        {ticks.map((t) => (
          <g key={t}>
            <line x1={PAD_LEFT} x2={WIDTH - PAD_RIGHT} y1={yFor(t)} y2={yFor(t)} className="chart-gridline" />
            <text x={PAD_LEFT - 6} y={yFor(t)} className="chart-tick-label" textAnchor="end" dominantBaseline="middle">
              {t}
            </text>
          </g>
        ))}

        {bridgePaths.map((d, k) => (
          <path key={`g${k}`} d={d} className="chart-line-gap" />
        ))}
        {linePaths.map((d, k) => (
          <path key={k} d={d} className="chart-line" />
        ))}
        {real.map((p) => (
          <circle key={`d${p.i}`} cx={xFor(p.i)} cy={yFor(p.value)} r="1.6" className="chart-dot" />
        ))}

        {showBaseline && (
          <line x1={PAD_LEFT} x2={WIDTH - PAD_RIGHT} y1={yFor(baselineAvg)} y2={yFor(baselineAvg)} className="chart-baseline" />
        )}

        <circle cx={xFor(last.i)} cy={yFor(last.value)} r="3" className="chart-marker" />

        {dayLabels.map((d) => (
          <text key={d.key} x={xFor(d.i)} y={HEIGHT - 6} className="chart-tick-label" textAnchor={d.anchor}>
            {formatDay(d.date)}
          </text>
        ))}
      </svg>
    </div>
  )
}
