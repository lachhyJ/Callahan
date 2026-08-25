function formatSleepDuration(seconds) {
  const h = Math.floor(seconds / 3600)
  const m = Math.round((seconds % 3600) / 60)
  return `${h}h ${m}m`
}

// Compact, non-interactive glance card - same footprint/placement pattern as
// WeeklyVolumeChart, but numbers instead of a chart since there's nothing to
// plot yet (that's the Phase 5 rolling-baseline trend). Renders only the
// stats Garmin actually returned for this date - a watch that doesn't
// report training readiness shouldn't show a permanent em-dash.
export default function WellnessCard({ wellness, todayIso }) {
  const stats = []
  if (wellness.sleepSeconds != null) {
    stats.push({ label: 'Sleep', value: formatSleepDuration(wellness.sleepSeconds) })
  }
  if (wellness.sleepScore != null) {
    stats.push({ label: 'Sleep score', value: wellness.sleepScore })
  }
  if (wellness.hrvLastNightAvg != null) {
    stats.push({ label: 'HRV', value: `${wellness.hrvLastNightAvg} ms` })
  }
  if (wellness.trainingReadinessScore != null) {
    stats.push({ label: 'Readiness', value: wellness.trainingReadinessScore })
  }

  if (stats.length === 0) return null

  const isStale = wellness.date !== todayIso

  return (
    <div className="wellness-card">
      <div className="wellness-card-header">
        <span className="volume-chart-label">Wellness</span>
        {isStale && <span className="wellness-card-stale">Yesterday</span>}
      </div>
      <div className="wellness-card-stats">
        {stats.map((s) => (
          <div key={s.label} className="wellness-card-stat">
            <span className="stat-label">{s.label}</span>
            <span className="stat-value wellness-card-stat-value">{s.value}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
