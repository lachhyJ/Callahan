// The Dashboard's one line about the next program review or test day, which comes from
// the Training calendar (GET /api/milestones/next). "T0 testing · Mon 24 Nov · in 14 days".
// Days are counted on the client so a cached response doesn't go stale overnight, and a
// milestone already past (a cache from last week) shows nothing.
export function milestoneLine(milestone, todayIso) {
  if (!milestone?.day || !milestone.title) return null
  const day = new Date(`${milestone.day}T00:00:00`)
  const today = new Date(`${todayIso}T00:00:00`)
  const days = Math.round((day - today) / 86_400_000)
  if (days < 0) return null

  // Built from parts: the locale's own joined format adds a comma after the weekday.
  const weekday = day.toLocaleDateString('en-AU', { weekday: 'short' })
  const month = day.toLocaleDateString('en-AU', { month: 'short' })
  const when = `${weekday} ${day.getDate()} ${month}`
  const away = days === 0 ? 'today' : days === 1 ? 'tomorrow' : `in ${days} days`
  return `${milestone.title} · ${when} · ${away}`
}
