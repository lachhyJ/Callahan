// Deliberately avoids toISOString(): it converts through UTC, so local
// midnight in any positive-UTC-offset timezone rolls back to the previous
// UTC day — every date would compute one day earlier than it actually is.
export function isoDate(d) {
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

// Monday-first week, matching the Calendar page's grid convention.
export function startOfWeek(d) {
  const day = (d.getDay() + 6) % 7 // Mon=0 ... Sun=6
  const start = new Date(d)
  start.setDate(d.getDate() - day)
  return start
}

export function endOfWeek(d) {
  const start = startOfWeek(d)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return end
}

// Short weekday + day-of-month, for lists already grouped under a week
// header that carries the month/date context (e.g. History) — avoids
// repeating the full date on every row while keeping the weekday, which is
// what's actually useful for recall ("that was a Tuesday run"). Deliberately
// skips an ordinal suffix (7th, 21st) — that's mostly visual flourish and
// adds clutter across a dense list.
export function shortWeekdayAndDay(isoDateStr) {
  const d = new Date(`${isoDateStr}T00:00:00`)
  const weekday = d.toLocaleDateString(undefined, { weekday: 'short' })
  return `${weekday} ${d.getDate()}`
}
