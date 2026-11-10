// The next program review or test day, from the Training calendar (GET /api/milestones/next).
// The Dashboard tints that day on its calendar grid, and the day sheet names it:
// "T0 testing · in 9 days". Days are counted on the client so a cached response doesn't go
// stale overnight, and a milestone already past (a cache from last week) is dropped.
export function upcomingMilestone(milestone, todayIso) {
  if (!milestone?.day || !milestone.title) return null
  const day = new Date(`${milestone.day}T00:00:00`)
  const today = new Date(`${todayIso}T00:00:00`)
  const days = Math.round((day - today) / 86_400_000)
  if (days < 0) return null

  const away = days === 0 ? 'today' : days === 1 ? 'tomorrow' : `in ${days} days`
  return { day: milestone.day, label: `${milestone.title} · ${away}` }
}
