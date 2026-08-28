const WIDTH = 300
const HEIGHT = 40
const PAD_Y = 5

// Compact, axisless trend for one wellness metric over the last few weeks.
// Non-interactive by design (a secondary glance next to the numbers, not a
// primary data view — that's the recovery timeline). The dashed rule is the
// 28-day baseline; the dot is the latest reading. `values` is a dense array
// aligned to consecutive days, with null for any day Garmin didn't report.
export default function WellnessSparkline({ values, baselineAvg }) {
  const real = values.map((v, i) => ({ v, i })).filter((p) => p.v != null)
  if (real.length < 5) return null

  const nums = real.map((p) => p.v)
  const lo = Math.min(...nums, baselineAvg ?? Infinity)
  const hi = Math.max(...nums, baselineAvg ?? -Infinity)
  const span = hi - lo || 1

  const x = (i) => (values.length <= 1 ? WIDTH / 2 : (i / (values.length - 1)) * WIDTH)
  const y = (v) => PAD_Y + (HEIGHT - 2 * PAD_Y) * (1 - (v - lo) / span)

  // Split into segments so a run of missing days lifts the pen rather than
  // drawing a straight line across the gap.
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

  const paths = segments
    .filter((s) => s.length > 0)
    .map((s) => s.map((p, k) => `${k === 0 ? 'M' : 'L'} ${x(p.i).toFixed(1)} ${y(p.v).toFixed(1)}`).join(' '))

  const last = real[real.length - 1]

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      className="wellness-sparkline"
      role="img"
      aria-label={`Trend over the last ${values.length} days`}
    >
      {baselineAvg != null && (
        <line x1="0" x2={WIDTH} y1={y(baselineAvg)} y2={y(baselineAvg)} className="wellness-sparkline-baseline" />
      )}
      {paths.map((d, k) => (
        <path key={k} d={d} className="wellness-sparkline-line" />
      ))}
      <circle cx={x(last.i)} cy={y(last.v)} r="3" className="wellness-sparkline-dot" />
    </svg>
  )
}
