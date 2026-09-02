import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import ChartGridLines from './ChartGridLines'

// Rendered to a string rather than into a DOM: the component emits SVG and has
// no behaviour, so react-dom/server is enough and the suite stays on plain node
// with no jsdom.
const render = (props) => renderToStaticMarkup(<svg><ChartGridLines {...props} /></svg>)

describe('ChartGridLines', () => {
  const base = { ticks: [0, 10, 20], y: (t) => 100 - t, x1: 30, x2: 300 }

  it('draws one line and one label per tick', () => {
    const out = render(base)
    expect(out.match(/<line/g)).toHaveLength(3)
    expect(out.match(/<text/g)).toHaveLength(3)
  })

  it('spans x1 to x2 and sits at the scaled y', () => {
    expect(render(base)).toContain('x1="30" x2="300" y1="90" y2="90"')
  })

  it('right-aligns the label just left of the axis', () => {
    // x1 - labelOffset, so the text ends before the gridline starts.
    expect(render(base)).toContain('x="24"')
    expect(render({ ...base, labelOffset: 4 })).toContain('x="26"')
  })

  it('formats labels through the supplied function', () => {
    expect(render({ ...base, label: (t) => `${t}kg` })).toContain('>20kg</text>')
  })

  it('lets a chart mark one tick differently', () => {
    // SeasonStrengthChart draws its zero line as a baseline, not a gridline.
    const out = render({ ...base, lineClassName: (t) => (t === 0 ? 'chart-baseline' : 'chart-gridline') })
    expect(out).toContain('class="chart-baseline"')
    expect(out.match(/chart-gridline/g)).toHaveLength(2)
  })

  it('defaults every line to the gridline class', () => {
    expect(render(base).match(/class="chart-gridline"/g)).toHaveLength(3)
  })

  it('renders nothing for an empty tick list', () => {
    expect(render({ ...base, ticks: [] })).toBe('<svg></svg>')
  })

  it('handles negative ticks, which the season chart produces', () => {
    expect(render({ ...base, ticks: [-10, 0], label: (t) => (t > 0 ? `+${t}` : t) })).toContain('>-10</text>')
  })
})
