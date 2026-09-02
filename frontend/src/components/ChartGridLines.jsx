// The horizontal gridline + left-hand tick label pair, which seven charts
// were each drawing with their own copy of the same four lines of SVG.
//
// The dimensions stay with the individual charts on purpose: WIDTH, HEIGHT and
// the PAD_* values differ per chart (90 to 168 tall, 20 to 34 of left padding)
// because each holds different content. They share a *shape*, not a size, so
// what's shared here is the drawing, not the geometry.
export default function ChartGridLines({
  ticks,
  y,
  x1,
  x2,
  label = (t) => t,
  labelOffset = 6,
  lineClassName = () => 'chart-gridline',
  keyPrefix = '',
}) {
  return ticks.map((t) => (
    <g key={`${keyPrefix}${t}`}>
      <line x1={x1} x2={x2} y1={y(t)} y2={y(t)} className={lineClassName(t)} />
      <text
        x={x1 - labelOffset}
        y={y(t)}
        className="chart-tick-label"
        textAnchor="end"
        dominantBaseline="middle"
      >
        {label(t)}
      </text>
    </g>
  ))
}
