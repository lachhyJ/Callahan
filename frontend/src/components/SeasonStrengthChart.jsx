import { useState } from 'react'
import { niceStep } from '../utils/chartScale'

const WIDTH = 340
const HEIGHT = 168
const PAD_LEFT = 32
const PAD_RIGHT = 30
const PAD_TOP = 16
const PAD_BOTTOM = 26

// Fallback only — used when the API returns no "primary" (compound) lifts.
const FALLBACK_VISIBLE = 4
// 5 dash patterns against 6 colours: colour+dash combos don't repeat until the
// 30th series, and the first few lines differ in texture as well as hue (Okabe-Ito
// has a weak amber/vermillion pair that colour alone doesn't separate well).
const DASHES = ['0', '5 3', '2 2', '9 3 2 3', '1 4']

function toDate(iso) {
  return new Date(`${iso}T00:00:00`)
}

function shortMonth(iso) {
  return toDate(iso).toLocaleDateString('en-AU', { month: 'short' })
}

function seriesVar(i) {
  return `var(--chart-series-${(i % 6) + 1})`
}

// Both axes share one time domain: month 0 starts at x=0, the month after the
// last month ends at x=N. A date maps to (whole months since month 0) +
// (fraction through its own month), so tournament weekends and the Nationals
// marker land on their real dates, not snapped to a month.
function makeXForDate(months) {
  const first = toDate(months[0].monthStart)
  const n = months.length
  return (d) => {
    const date = typeof d === 'string' ? toDate(d) : d
    const wholeMonths = (date.getFullYear() - first.getFullYear()) * 12 + (date.getMonth() - first.getMonth())
    const daysInMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()
    const frac = (date.getDate() - 1) / daysInMonth
    const pos = Math.max(0, Math.min(n, wholeMonths + frac))
    return PAD_LEFT + (pos / n) * (WIDTH - PAD_LEFT - PAD_RIGHT)
  }
}

export default function SeasonStrengthChart({ data }) {
  const { months, series, seasons, bands } = data
  const n = months.length
  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM
  const bottom = PAD_TOP + plotHeight

  // Start with the program's compound lifts (isPrimary) visible; deeper slots
  // and isolation work are toggled on from the legend. If the API flagged
  // none primary, fall back to the first few.
  const [hidden, setHidden] = useState(() => {
    const anyPrimary = series.some((s) => s.isPrimary)
    const startHidden = anyPrimary
      ? series.filter((s) => !s.isPrimary)
      : series.slice(FALLBACK_VISIBLE)
    return new Set(startHidden.map((s) => s.exerciseId))
  })
  const toggle = (id) =>
    setHidden((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const visibleSeries = series.filter((s) => !hidden.has(s.exerciseId))

  const xForDate = makeXForDate(months)
  const xMonthCentre = (i) => PAD_LEFT + ((i + 0.5) / n) * plotWidth

  // Left axis — percent from baseline, always including 0.
  const pcts = visibleSeries.flatMap((s) => s.points.map((p) => Number(p.pctFromBaseline)))
  const rawMin = Math.min(0, ...pcts)
  const rawMax = Math.max(0, ...pcts)
  const pctPad = Math.max(2, (rawMax - rawMin) * 0.15)
  const yMinPct = rawMin - pctPad
  const yMaxPct = rawMax + pctPad
  const pctStep = niceStep(yMaxPct - yMinPct)
  const pctTicks = []
  for (let t = Math.ceil(yMinPct / pctStep) * pctStep; t <= yMaxPct + 1e-9; t += pctStep) {
    pctTicks.push(Math.round(t))
  }
  const yPct = (v) => bottom - ((v - yMinPct) / (yMaxPct - yMinPct)) * plotHeight

  // Right axis — run km per month, zero-based.
  const maxRun = Math.max(1, ...months.map((m) => Number(m.runKm)))
  const runStep = niceStep(maxRun)
  const runMax = Math.max(runStep, Math.ceil(maxRun / runStep) * runStep)
  const yRun = (v) => bottom - (v / runMax) * plotHeight
  const anyRun = months.some((m) => Number(m.runKm) > 0)

  // Ultimate live-play — context only, its own hidden scale.
  const maxUlt = Math.max(1, ...months.map((m) => m.ultimateLivePlayMin))
  const anyUlt = months.some((m) => m.ultimateLivePlayMin > 0)
  const ultLine = months
    .map((m, i) => `${i === 0 ? 'M' : 'L'} ${xMonthCentre(i).toFixed(1)} ${(bottom - (m.ultimateLivePlayMin / maxUlt) * plotHeight * 0.9).toFixed(1)}`)
    .join(' ')

  const runLine = months
    .map((m, i) => `${i === 0 ? 'M' : 'L'} ${xMonthCentre(i).toFixed(1)} ${yRun(Number(m.runKm)).toFixed(1)}`)
    .join(' ')

  const linePath = (points) =>
    points
      .map((p) => {
        const i = months.findIndex((m) => m.monthStart === p.monthStart)
        return { x: xMonthCentre(i), y: yPct(Number(p.pctFromBaseline)) }
      })
      .map((pt, k) => `${k === 0 ? 'M' : 'L'} ${pt.x.toFixed(1)} ${pt.y.toFixed(1)}`)
      .join(' ')

  const nationals = seasons.map((s) => s.targetDate).filter(Boolean)

  return (
    <div className="trend-chart">
      <h2 className="trend-chart-title">Strength through the season</h2>

      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="trend-chart-svg"
        style={{ height: 'auto' }}
        role="img"
        aria-label="Lift e1RM percent change from baseline, with season and tournament periods and monthly running distance"
      >
        {/* season spans */}
        {seasons.map((s, k) => {
          const x = xForDate(s.start)
          const xe = xForDate(s.end)
          return (
            <g key={`season${k}`}>
              <rect x={x} y={PAD_TOP} width={Math.max(1, xe - x)} height={plotHeight} className="season-band" />
              <line x1={x} x2={x} y1={PAD_TOP} y2={PAD_TOP + plotHeight} className="season-band-edge" />
              <line x1={xe} x2={xe} y1={PAD_TOP} y2={PAD_TOP + plotHeight} className="season-band-edge" />
              <text x={x + 3} y={PAD_TOP + 9} className="season-band-label">{s.name}</text>
            </g>
          )
        })}

        {/* tournament weekends */}
        {bands.map((b, k) => {
          const x = xForDate(b.start)
          const w = Math.max(1.5, xForDate(b.end) - x)
          return <rect key={`band${k}`} x={x} y={PAD_TOP} width={w} height={plotHeight} className="load-tournament-band" />
        })}

        {/* percent gridlines + left ticks */}
        {pctTicks.map((t) => (
          <g key={`p${t}`}>
            <line
              x1={PAD_LEFT}
              x2={WIDTH - PAD_RIGHT}
              y1={yPct(t)}
              y2={yPct(t)}
              className={t === 0 ? 'chart-baseline' : 'chart-gridline'}
            />
            <text x={PAD_LEFT - 4} y={yPct(t)} className="chart-tick-label" textAnchor="end" dominantBaseline="middle">
              {t > 0 ? `+${t}` : t}
            </text>
          </g>
        ))}

        {/* run distance — right axis, teal, deliberately recessive */}
        {anyRun && (
          <>
            <path d={runLine} className="season-run-line" />
            {[runMax, runMax / 2].map((t) => (
              <text
                key={`rt${t}`}
                x={WIDTH - PAD_RIGHT + 4}
                y={yRun(t)}
                className="chart-tick-label chart-tick-label-run"
                textAnchor="start"
                dominantBaseline="middle"
              >
                {Math.round(t)}
              </text>
            ))}
            <text x={WIDTH - PAD_RIGHT + 4} y={PAD_TOP - 5} className="chart-tick-label chart-tick-label-run" textAnchor="start">
              km/mo
            </text>
          </>
        )}

        {/* ultimate live-play context line */}
        {anyUlt && <path d={ultLine} className="season-ultimate-line" />}

        {/* lift trajectories */}
        {visibleSeries.map((s) => {
          const idx = series.findIndex((x) => x.exerciseId === s.exerciseId)
          return (
            <path
              key={s.exerciseId}
              d={linePath(s.points)}
              className={`chart-line chart-series-${(idx % 6) + 1}`}
              style={{ strokeDasharray: DASHES[idx % DASHES.length] }}
            />
          )
        })}

        {/* Nationals markers */}
        {nationals.map((d, k) => {
          const x = xForDate(d)
          return (
            <g key={`nat${k}`}>
              <line x1={x} x2={x} y1={PAD_TOP} y2={bottom} className="season-nationals-marker" />
              <text x={x} y={PAD_TOP - 5} className="season-nationals-label" textAnchor="middle">Nationals</text>
            </g>
          )
        })}

        {/* month labels */}
        {months.map((m, i) => (
          <text key={`m${i}`} x={xMonthCentre(i)} y={HEIGHT - 6} className="season-month-label" textAnchor="middle">
            {shortMonth(m.monthStart)}
          </text>
        ))}
      </svg>

      <div className="trend-chart-legend" style={{ flexWrap: 'wrap' }}>
        {series.map((s) => {
          const idx = series.findIndex((x) => x.exerciseId === s.exerciseId)
          const off = hidden.has(s.exerciseId)
          return (
            <button
              key={s.exerciseId}
              type="button"
              className={`trend-legend-item is-toggle${off ? ' is-off' : ''}`}
              onClick={() => toggle(s.exerciseId)}
              aria-pressed={!off}
            >
              <span className="trend-legend-swatch series" style={{ background: seriesVar(idx) }} />
              {s.exerciseName}
            </button>
          )
        })}
      </div>

      <div className="trend-chart-legend season-strength-keys">
        {seasons.length > 0 && (
          <span className="trend-legend-item"><span className="trend-legend-swatch key-season" /> season</span>
        )}
        {bands.length > 0 && (
          <span className="trend-legend-item"><span className="trend-legend-swatch key-tournament" /> tournament</span>
        )}
        {nationals.length > 0 && (
          <span className="trend-legend-item"><span className="trend-legend-swatch key-nationals" /> Nationals</span>
        )}
        {anyRun && (
          <span className="trend-legend-item"><span className="trend-legend-swatch key-run" /> km run/mo</span>
        )}
        {anyUlt && (
          <span className="trend-legend-item"><span className="trend-legend-swatch key-ultimate" /> Ultimate live-play</span>
        )}
      </div>

      <span className="trend-chart-caption">
        % change in each lift&rsquo;s best e1RM vs its first month in view
      </span>
    </div>
  )
}
