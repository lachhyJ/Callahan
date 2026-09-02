import { useRef, useState } from 'react'
import { buildTicks, niceStep } from '../utils/chartScale'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 150
const PAD_LEFT = 34
const PAD_RIGHT = 12
const PAD_TOP = 16
const PAD_BOTTOM = 28

function formatDay(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('en-AU', { day: 'numeric', month: 'short' })
}

// Per-game value across a tournament, in chronological order — a line + area +
// crosshair chart modelled on ProgressionChart. Deliberately one series: the
// totals/rates switch lives in the parent, which rebuilds `points` and passes
// a matching `unitLabel` plus a `caption` naming what the line plots.
// Expects points.length >= 2 (parent guards).
export default function TournamentGameChart({ points, unitLabel, caption }) {
  const svgRef = useRef(null)
  const [activeIdx, setActiveIdx] = useState(points.length - 1)

  const values = points.map((p) => p.value)
  const rawMax = Math.max(...values)
  const yMin = 0
  const step = niceStep(rawMax || 1)
  const yMax = Math.max(step, Math.ceil(rawMax / step) * step)

  const ticks = buildTicks(0, yMax, step, 1)

  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM

  const xFor = (i) => PAD_LEFT + (i / (points.length - 1)) * plotWidth
  const yFor = (v) => PAD_TOP + plotHeight - ((v - yMin) / (yMax - yMin)) * plotHeight

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i)} ${yFor(p.value)}`).join(' ')
  const areaPath = `${linePath} L ${xFor(points.length - 1)} ${PAD_TOP + plotHeight} L ${xFor(0)} ${PAD_TOP + plotHeight} Z`

  function handlePointerMove(e) {
    const svg = svgRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const clientX = e.touches ? e.touches[0].clientX : e.clientX
    const svgX = ((clientX - rect.left) / rect.width) * WIDTH
    let nearest = 0
    let nearestDist = Infinity
    points.forEach((p, i) => {
      const dist = Math.abs(xFor(i) - svgX)
      if (dist < nearestDist) {
        nearestDist = dist
        nearest = i
      }
    })
    setActiveIdx(nearest)
  }

  const active = points[activeIdx]
  const activeX = xFor(activeIdx)
  const activeY = yFor(active.value)
  const tooltipRight = activeX > WIDTH * 0.6
  const roundedActive = Math.round(active.value * 10) / 10

  return (
    <div className="progression-chart">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="progression-chart-svg"
        onPointerMove={handlePointerMove}
        onPointerLeave={() => setActiveIdx(points.length - 1)}
        onTouchMove={handlePointerMove}
      >
        <ChartGridLines ticks={ticks} y={yFor} x1={PAD_LEFT} x2={WIDTH - PAD_RIGHT} />

        <path d={areaPath} className="chart-area" />
        <path d={linePath} className="chart-line" />

        {points.map((p, i) => {
          // One label per day — repeating "28 Feb" under three games on the
          // same date just collides. Anchor it at the day's first game.
          if (i > 0 && p.date === points[i - 1].date) return null
          return (
            <text
              key={p.date + i}
              x={xFor(i)}
              y={HEIGHT - 8}
              className="chart-tick-label"
              textAnchor={i === 0 ? 'start' : i === points.length - 1 ? 'end' : 'middle'}
            >
              {formatDay(p.date)}
            </text>
          )
        })}

        <line x1={activeX} x2={activeX} y1={PAD_TOP} y2={PAD_TOP + plotHeight} className="chart-crosshair" />
        <circle cx={activeX} cy={activeY} r="4" className="chart-marker" />
      </svg>

      {caption && <span className="trend-chart-caption">{caption}</span>}

      <div
        className="chart-tooltip"
        style={{
          left: `${(activeX / WIDTH) * 100}%`,
          transform: tooltipRight ? 'translateX(-100%)' : 'none',
        }}
      >
        <strong>{roundedActive} {unitLabel}</strong>
        <span>{formatDay(active.date)}</span>
      </div>
    </div>
  )
}
