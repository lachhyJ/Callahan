import { Link } from 'react-router-dom'
import { ChevronRightIcon } from '../icons'
import WellnessSparkline from './WellnessSparkline'

function formatSleepDuration(seconds) {
  const h = Math.floor(seconds / 3600)
  const m = Math.round((seconds % 3600) / 60)
  return `${h}h ${m}m`
}

// Compact glance card on the dashboard - the raw same-day Garmin numbers, plus
// (once there's ~a week of history) a one-line plain-language read against the
// rolling baseline. The card links to /wellness for the full per-metric
// breakdown. Renders only the stats Garmin actually returned for this date - a
// watch that doesn't report training readiness shouldn't show a permanent
// em-dash.
export default function WellnessCard({ wellness, todayIso, insight, readinessSeries }) {
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

  const headline = insight?.hasEnoughHistory ? insight.headline : null
  const readinessBaseline = insight?.metrics?.find((m) => m.key === 'readiness')?.baselineAvg
  if (stats.length === 0 && !headline) return null

  const isStale = wellness.date !== todayIso

  return (
    <Link to="/wellness" className="wellness-card wellness-card-link">
      <div className="wellness-card-header">
        <span className="volume-chart-label">Wellness</span>
        <span className="wellness-card-header-right">
          {isStale && <span className="wellness-card-stale">Yesterday</span>}
          <ChevronRightIcon />
        </span>
      </div>
      {stats.length > 0 && (
        <div className="wellness-card-stats">
          {stats.map((s) => (
            <div key={s.label} className="wellness-card-stat">
              <span className="stat-label">{s.label}</span>
              <span className="stat-value wellness-card-stat-value">{s.value}</span>
            </div>
          ))}
        </div>
      )}
      {readinessSeries && (
        <div className="wellness-card-sparkline">
          <span className="stat-label">Readiness · last {readinessSeries.length} days</span>
          <WellnessSparkline values={readinessSeries} baselineAvg={readinessBaseline} />
        </div>
      )}
      {headline && <p className="wellness-card-insight">{headline}</p>}
    </Link>
  )
}
