export function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
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
// definition for the "13 pts · 48% on" string.
export function onFieldTeaser(activity) {
  if (activity.type !== 'Ultimate' || activity.pointsPlayed == null) return null
  const total = activity.onFieldSeconds + activity.offFieldSeconds + (activity.mixedSeconds ?? 0)
  if (!total) return null
  const onPct = Math.round((activity.onFieldSeconds / total) * 100)
  return `${activity.pointsPlayed} pt${activity.pointsPlayed === 1 ? '' : 's'} · ${onPct}% on`
}
