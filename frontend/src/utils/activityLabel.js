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
