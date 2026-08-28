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
