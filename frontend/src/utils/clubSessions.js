// Display helpers for the club-session list and page.

export const ZONE_LABELS = ['Zone 1', 'Zone 2', 'Zone 3', 'Zone 4', 'Zone 5']

// Figures shown side by side, in list order. Each returns a display string or
// null (rendered as a dash) so a session with no track or no HR still lines up.
export const CLUB_COLUMNS = [
  { key: 'km', label: 'km', value: (s) => (s.distanceKm == null ? null : Number(s.distanceKm).toFixed(1)) },
  { key: 'hr', label: 'Avg HR', value: (s) => s.avgHeartRate },
  { key: 'load', label: 'Load', value: (s) => (s.trainingLoad == null ? null : Math.round(s.trainingLoad)) },
  { key: 'sprints', label: 'Sprints', value: (s) => s.sprintCount },
]

// Share of session time in each zone, for the zone bar. Null when Garmin gave
// no zones. Percentages come from the zone total, not the session duration:
// time below zone 1 isn't reported, so the bar is "of the time in a zone".
export function zoneShares(zoneSeconds) {
  if (!zoneSeconds) return null
  const total = zoneSeconds.reduce((a, b) => a + b, 0)
  if (!total) return null
  return zoneSeconds.map((sec) => ({ sec, pct: (sec / total) * 100 }))
}

// How a figure compares with the mean of the other sessions that have it: a
// signed whole-number percent, or null with fewer than two sessions to
// compare. Used on the detail page so a number reads as "above or below my
// usual" rather than as a bare absolute.
export function versusUsual(sessions, current, pick) {
  const others = sessions.filter((s) => s.id !== current.id).map(pick).filter((v) => v != null)
  const mine = pick(current)
  if (mine == null || others.length < 2) return null
  const mean = others.reduce((a, b) => a + Number(b), 0) / others.length
  if (!mean) return null
  return Math.round(((Number(mine) - mean) / mean) * 100)
}
