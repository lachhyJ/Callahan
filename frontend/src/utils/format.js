// Formatters and label maps that were each defined identically in three
// different files. Nothing here is domain logic — it's presentation, and the
// point of collecting it is that "how do we render a weight" should have one
// answer rather than three that happen to agree.

export const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

// Single-letter set-type badges. Normal is deliberately blank — the common
// case shouldn't carry a marker.
export const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }

// "12.5" / "60" — drops a trailing .0 so whole-kilo lifts don't read as
// decimals, keeps one place when there genuinely is a half-kilo.
export function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

// "15.1k" / "840" — chart-axis and summary volumes, where the exact kilo is
// noise and the magnitude is the point.
export function formatVolume(v) {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(Math.round(v))
}

// "2:05" — a duration in seconds as m:ss. Used for rest countdowns and for
// elapsed session time.
export function formatClock(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

// "Sep" — the short month for a chart axis or a trend row, from an ISO date.
// Locale-default deliberately (unlike the pinned en-AU date formatters): a
// bare month abbreviation reads correctly in any of them.
export function formatMonthShort(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}
