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
    const label = activity.notes?.trim() || 'Ultimate'
    return `${label} · ${duration}`
  }
  return `${activity.type} · ${duration}`
}

// A one-glance teaser for the on/off-field data behind the game-detail link
// - cheap enough to compute inline that the feature doesn't stay hidden
// behind a tap. Undefined (not just null) whenever the fields aren't there,
// so it never renders "0 pts" for pre-backfill or unclassified games. Shared
// by ActivitySessionRow (History/Dashboard) and GamesListPage - one
// definition for the "13 pts · 48% on field · 24% live" string. "on field" is
// field-occupancy time (includes between-point standing); "live" is time
// inside a detected point - see backend/decisions "on-field time measures
// field occupancy" and "the active-play figure".
export function onFieldTeaser(activity) {
  if (activity.type !== 'Ultimate' || activity.pointsPlayed == null) return null
  const total = activity.onFieldSeconds + activity.offFieldSeconds + (activity.mixedSeconds ?? 0)
  if (!total) return null
  const onPct = Math.round((activity.onFieldSeconds / total) * 100)
  const pts = `${activity.pointsPlayed} pt${activity.pointsPlayed === 1 ? '' : 's'}`
  if (activity.livePlaySeconds == null) return `${pts} · ${onPct}% on field`
  const livePct = Math.round((activity.livePlaySeconds / total) * 100)
  return `${pts} · ${onPct}% on field · ${livePct}% live`
}
