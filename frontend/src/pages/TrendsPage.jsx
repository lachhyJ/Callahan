import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getLiftTrends, getRunTypeTrends, getTrends } from '../api/client'
import ConsistencyTrendChart from '../components/ConsistencyTrendChart'
import VolumeTrendChart from '../components/VolumeTrendChart'
import LiftTrendsList from '../components/LiftTrendsList'
import RunTypeTrendsList from '../components/RunTypeTrendsList'

function formatVolume(v) {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}k`
  return String(Math.round(v))
}

// months is always the fixed 6-month window from getTrends(6) — split cleanly
// into two 3-month halves to compare against each other.
function periodSummary(months) {
  const recent = months.slice(3)
  const prior = months.slice(0, 3)
  const sum = (list, key) => list.reduce((total, m) => total + m[key], 0)
  const sessions = (list) => sum(list, 'gymSessions') + sum(list, 'runSessions')

  return {
    recentSessions: sessions(recent),
    priorSessions: sessions(prior),
    recentVolume: sum(recent, 'volumeKg'),
    priorVolume: sum(prior, 'volumeKg'),
  }
}

export default function TrendsPage() {
  const [months, setMonths] = useState(null)
  const [liftTrends, setLiftTrends] = useState(null)
  const [runTypeTrends, setRunTypeTrends] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getTrends(6).then(setMonths).catch((err) => setError(err.message))
    getLiftTrends(6).then(setLiftTrends).catch(() => setLiftTrends([]))
    getRunTypeTrends(6).then(setRunTypeTrends).catch(() => setRunTypeTrends([]))
  }, [])

  const hasAnyData = months?.some((m) => m.gymSessions > 0 || m.runSessions > 0)
  const summary = months && hasAnyData ? periodSummary(months) : null

  return (
    <main className="page">
      <h1>Trends</h1>

      {error && <p className="error">{error}</p>}
      {!error && months === null && <p>Loading…</p>}

      {months && !hasAnyData && (
        <div className="empty-state">
          <p>Not enough history yet — trends need a few months of sessions to say anything useful.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}

      {months && hasAnyData && (
        <>
          <ConsistencyTrendChart months={months} />
          <p className="trend-summary">
            {summary.recentSessions} sessions in the last 3 months
            {summary.priorSessions > 0 && ` (${summary.priorSessions} in the 3 before that)`}
          </p>

          <VolumeTrendChart months={months} />
          <p className="trend-summary">
            {formatVolume(summary.recentVolume)} kg lifted in the last 3 months
            {summary.priorVolume > 0 && ` (${formatVolume(summary.priorVolume)} kg in the 3 before that)`}
          </p>

          {liftTrends && liftTrends.length > 0 && (
            <div className="section-gap">
              <h2 className="trend-chart-title">Lift trends</h2>
              <LiftTrendsList trends={liftTrends} />
            </div>
          )}

          {runTypeTrends && runTypeTrends.length > 0 && (
            <div className="section-gap">
              <h2 className="trend-chart-title">Run mix</h2>
              <RunTypeTrendsList trends={runTypeTrends} />
            </div>
          )}
        </>
      )}
    </main>
  )
}
