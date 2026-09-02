// A "nice" axis step for a given value range - rounds the rough range/3 up to
// the nearest 1 / 2.5 / 5 / 10 in the right order of magnitude. Extracted from
// ProgressionChart and VolumeTrendChart, which each carried their own copy.
export function niceStep(range) {
  const rough = range / 3
  const magnitude = 10 ** Math.floor(Math.log10(rough || 1))
  const normalized = rough / magnitude
  const step = normalized < 1.5 ? 1 : normalized < 3.5 ? 2.5 : normalized < 7.5 ? 5 : 10
  return step * magnitude
}

// Axis ticks from `min` up to `max` in `step` increments, starting at the first
// multiple of step at or above min. The epsilon absorbs the float drift of
// repeated addition (without it a final tick that should land exactly on max
// can be dropped).
//
// `decimals` is how far to round each label, and it is not cosmetic: the season
// chart wants whole percentages (0), most charts want 1dp, and the volume chart
// needs 2 because a degenerate month can produce a 0.25 step, which 1dp would
// render as 0.3 / 0.8. Each caller passes what its own loop used to do, so the
// tick values are unchanged from before this was extracted.
export function buildTicks(min, max, step, decimals = 1) {
  const factor = 10 ** decimals
  const ticks = []
  for (let t = Math.ceil(min / step) * step; t <= max + 1e-9; t += step) {
    ticks.push(Math.round(t * factor) / factor)
  }
  return ticks
}

// Maps a data value onto an SVG y coordinate: `min` sits at the bottom of the
// plot, `max` at the top, y growing downwards. Three charts carried this same
// expression inline.
export function linearScale({ min, max, top, height }) {
  const span = max - min
  return (v) => top + height - (span === 0 ? 0 : ((v - min) / span) * height)
}
