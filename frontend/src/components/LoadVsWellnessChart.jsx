import { niceStep } from '../utils/chartScale'
import ChartGridLines from './ChartGridLines'
import { formatVolume } from '../utils/format'

const WIDTH = 320
const HEIGHT = 120
const PAD_LEFT = 30
const PAD_RIGHT = 26
const PAD_TOP = 10
const PAD_BOTTOM = 20


function formatWeek(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('en-AU', { day: 'numeric', month: 'short' })
}

// Weekly gym volume (grey bars, left axis) with mean readiness laid over it
// (accent line, right axis); tournament weeks shaded. Descriptive only — it
// shows whether recovery moved with load, it doesn't prescribe anything.
// Non-interactive for v1; a metric switch (HRV / sleep) and a crosshair are
// deferred.
export default function LoadVsWellnessChart({ weeks }) {
  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM
  const bottom = PAD_TOP + plotHeight

  // Left axis: gym volume, zero-based.
  const maxVol = Math.max(...weeks.map((w) => w.gymVolume), 1)
  const volStep = niceStep(maxVol)
  const volMax = Math.max(volStep, Math.ceil(maxVol / volStep) * volStep)
  const volTicks = []
  for (let t = 0; t <= volMax + 1e-9; t += volStep) volTicks.push(t)
  const yVol = (v) => bottom - (v / volMax) * plotHeight

  // Right axis: mean readiness, padded so a narrow band still reads.
  const readVals = weeks.map((w) => w.meanReadiness).filter((v) => v != null)
  const hasReadiness = readVals.length >= 2
  const rMin = hasReadiness ? Math.min(...readVals) : 0
  const rMax = hasReadiness ? Math.max(...readVals) : 100
  const rPad = Math.max(2, (rMax - rMin) * 0.2 || 5)
  const readMin = Math.max(0, rMin - rPad)
  const readMax = Math.min(100, rMax + rPad)
  const yRead = (v) => bottom - ((v - readMin) / (readMax - readMin || 1)) * plotHeight

  const slot = plotWidth / weeks.length
  const barWidth = Math.max(3, slot * 0.6)
  const xCenter = (i) => PAD_LEFT + slot * i + slot / 2

  // Readiness line, split on missing weeks.
  const pts = weeks.map((w, i) => ({ v: w.meanReadiness, i })).filter((p) => p.v != null)
  const segments = []
  let cur = []
  let prev = null
  for (const p of pts) {
    if (prev != null && p.i !== prev + 1) {
      segments.push(cur)
      cur = []
    }
    cur.push(p)
    prev = p.i
  }
  if (cur.length) segments.push(cur)
  const linePaths = segments
    .filter((s) => s.length >= 2)
    .map((s) => s.map((p, k) => `${k === 0 ? 'M' : 'L'} ${xCenter(p.i).toFixed(1)} ${yRead(p.v).toFixed(1)}`).join(' '))
  const lastRead = pts[pts.length - 1]
  const anyTournament = weeks.some((w) => w.isTournamentWeek)

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Recovery vs load</h2>

      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="trend-chart-svg load-vs-wellness-svg"
        role="img"
        aria-label="Weekly gym volume with mean readiness overlaid"
      >
        <ChartGridLines
          ticks={volTicks}
          y={yVol}
          x1={PAD_LEFT}
          x2={WIDTH - PAD_RIGHT}
          label={formatVolume}
          labelOffset={4}
          keyPrefix="v"
        />

        {weeks.map((w, i) =>
          w.isTournamentWeek ? (
            <rect key={`t${i}`} x={PAD_LEFT + slot * i} y={PAD_TOP} width={slot} height={plotHeight} className="load-tournament-band" />
          ) : null
        )}

        {weeks.map((w, i) => {
          const h = Math.max(w.gymVolume > 0 ? 2 : 0, (w.gymVolume / volMax) * plotHeight)
          return (
            <rect
              key={`b${i}`}
              x={xCenter(i) - barWidth / 2}
              y={bottom - h}
              width={barWidth}
              height={h}
              rx={Math.min(2, barWidth / 2)}
              className={i === weeks.length - 1 ? 'trend-bar current' : 'trend-bar'}
            />
          )
        })}

        {hasReadiness && (
          <>
            {linePaths.map((d, k) => (
              <path key={`l${k}`} d={d} className="chart-line" />
            ))}
            {lastRead && <circle cx={xCenter(lastRead.i)} cy={yRead(lastRead.v)} r="3" className="chart-marker" />}
            {[readMin, readMax].map((t) => (
              <text
                key={`r${t}`}
                x={WIDTH - PAD_RIGHT + 4}
                y={yRead(t)}
                className="chart-tick-label chart-tick-label-accent"
                textAnchor="start"
                dominantBaseline="middle"
              >
                {Math.round(t)}
              </text>
            ))}
          </>
        )}

        <text x={PAD_LEFT} y={HEIGHT - 4} className="chart-tick-label" textAnchor="start">
          {formatWeek(weeks[0].weekStart)}
        </text>
        <text x={WIDTH - PAD_RIGHT} y={HEIGHT - 4} className="chart-tick-label" textAnchor="end">
          {formatWeek(weeks[weeks.length - 1].weekStart)}
        </text>
      </svg>

      <span className="trend-chart-caption">
        kg lifted per week · line is mean readiness{anyTournament ? ' · shaded weeks had a tournament' : ''}
      </span>
    </div>
  )
}
