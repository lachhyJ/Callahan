import { useRef, useState } from 'react'
import { buildTicks, niceStep } from '../utils/chartScale'
import ChartGridLines from './ChartGridLines'

const WIDTH = 320
const HEIGHT = 160
const PAD_LEFT = 34
const PAD_RIGHT = 12
const PAD_TOP = 16
const PAD_BOTTOM = 22

function formatDate(iso) {
  const d = new Date(`${iso}T00:00:00`)
  return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
}

// Expects points.length >= 2 — callers should show a simpler message below that.
export default function ProgressionChart({ points }) {
  const svgRef = useRef(null)
  const [activeIdx, setActiveIdx] = useState(points.length - 1)

  const values = points.map((p) => p.maxWeightKg)
  const rawMin = Math.min(...values)
  const rawMax = Math.max(...values)
  const pad = Math.max(1, (rawMax - rawMin) * 0.2 || rawMax * 0.1 || 1)
  const yMin = Math.max(0, rawMin - pad)
  const yMax = rawMax + pad

  const step = niceStep(yMax - yMin)
  const ticks = buildTicks(yMin, yMax, step, 1)

  const plotWidth = WIDTH - PAD_LEFT - PAD_RIGHT
  const plotHeight = HEIGHT - PAD_TOP - PAD_BOTTOM

  const xFor = (i) => PAD_LEFT + (points.length === 1 ? plotWidth / 2 : (i / (points.length - 1)) * plotWidth)
  const yFor = (v) => PAD_TOP + plotHeight - ((v - yMin) / (yMax - yMin)) * plotHeight

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i)} ${yFor(p.maxWeightKg)}`).join(' ')
  const areaPath = `${linePath} L ${xFor(points.length - 1)} ${PAD_TOP + plotHeight} L ${xFor(0)} ${PAD_TOP + plotHeight} Z`

  function handlePointerMove(e) {
    const svg = svgRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const clientX = e.touches ? e.touches[0].clientX : e.clientX
    const ratio = (clientX - rect.left) / rect.width
    const svgX = ratio * WIDTH
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
  const activeY = yFor(active.maxWeightKg)
  const tooltipRight = activeX > WIDTH * 0.6
  const isLast = activeIdx === points.length - 1

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

        <line x1={activeX} x2={activeX} y1={PAD_TOP} y2={PAD_TOP + plotHeight} className="chart-crosshair" />
        <circle cx={activeX} cy={activeY} r="4" className="chart-marker" />

        <text x={xFor(points.length - 1)} y={yFor(points[points.length - 1].maxWeightKg) - 10} className="chart-end-label" textAnchor="end">
          {points[points.length - 1].maxWeightKg} kg
        </text>
      </svg>

      <div
        className="chart-tooltip"
        style={{
          left: `${(activeX / WIDTH) * 100}%`,
          transform: tooltipRight ? 'translateX(-100%)' : 'none',
        }}
      >
        <strong>{active.maxWeightKg} kg</strong>
        <span>{formatDate(active.date)}{isLast ? ' (latest)' : ''}</span>
      </div>
    </div>
  )
}
