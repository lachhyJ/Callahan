export function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

// "1h 12min" / "48min" — a rounded-to-the-minute duration for field-time
// totals and legends, where seconds precision is noise. Shared by the game
// detail page, the field split bar and the tournament roll-up.
export function formatHoursMinutes(totalSeconds) {
  const minutes = Math.round(totalSeconds / 60)
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return hours > 0 ? `${hours}h ${mins}min` : `${mins}min`
}

// Centralizes per-type display text so Ultimate (no distance) never renders
// a "0 km" or otherwise distance-shaped placeholder alongside a real run.
export function activityLabel(activity) {
  const duration = formatDuration(activity.durationSeconds)
  if (activity.type === 'Running') {
    const label = activity.activitySessionTypeName ?? 'Run'
    return `${label} · ${activity.distanceKm} km in ${duration}`
  }
  if (activity.type === 'Ultimate') {
    // Garmin's auto-title for the sport is "<City> Ultimate Disc" (e.g.
    // "Melbourne Ultimate Disc") - not a name Lachlan chose, so treat it as
    // unnamed. A title he set himself is kept as-is.
    let name = activity.notes?.trim() || 'Ultimate'
    if (/ultimate disc$/i.test(name)) name = 'Ultimate'
    const category = activity.activitySessionTypeName
    if (!category) return `${name} · ${duration}`
    // Don't render "Pod · Pod" when the Garmin event was named the same as the
    // category we assigned.
    if (name.toLowerCase() === category.toLowerCase()) return `${category} · ${duration}`
    return `${name} · ${category} · ${duration}`
  }
  return `${activity.type} · ${duration}`
}

// A one-glance teaser for the live-play data behind the game-detail link -
// cheap enough to compute inline that the feature doesn't stay hidden behind
// a tap. Null (never "0 pts") whenever the fields aren't there, so it stays
// blank for pre-backfill or unclassified games. Shared by ActivitySessionRow
// (History/Dashboard) and GameRow (games + tournament lists) - one definition
// for the "14 pts · 24% live play" string. "Live play" is on-field time
// inside a detected point, as opposed to waiting on the line between points;
// see backend/decisions "the active-play figure".
export function livePlayTeaser(activity) {
  if (activity.type !== 'Ultimate' || activity.pointsPlayed == null) return null
  const pts = `${activity.pointsPlayed} pt${activity.pointsPlayed === 1 ? '' : 's'}`
  if (activity.livePlaySeconds == null) return pts
  const total = activity.onFieldSeconds + activity.offFieldSeconds + (activity.mixedSeconds ?? 0)
  if (!total) return pts
  const livePct = Math.round((activity.livePlaySeconds / total) * 100)
  return `${pts} · ${livePct}% live play`
}
