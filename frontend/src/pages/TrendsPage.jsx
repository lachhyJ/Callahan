import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getLiftTrends, getLoadTrend, getRunTypeTrends, getSeasonStrength, getTrends } from '../api/client'
import ConsistencyTrendChart from '../components/ConsistencyTrendChart'
import VolumeTrendChart from '../components/VolumeTrendChart'
import LiftTrendsList from '../components/LiftTrendsList'
import RunTypeTrendsList from '../components/RunTypeTrendsList'
import LoadVsWellnessChart from '../components/LoadVsWellnessChart'
import SeasonStrengthChart from '../components/SeasonStrengthChart'
import MuscleBalanceSection from '../components/MuscleBalanceSection'
import { formatVolume } from '../utils/format'

const SEASON_STRENGTH_MONTHS = 9

function seasonStrengthSummary(data) {
  const primary = data.series.filter((s) => s.isPrimary)
  const pick = (primary.length > 0 ? primary : data.series).slice(0, 3)
  const top = pick.map((s) => {
    const pct = Math.round(Number(s.points[s.points.length - 1].pctFromBaseline))
    return `${s.exerciseName} ${pct >= 0 ? '+' : ''}${pct}%`
  })
  if (top.length === 0) return null
  let line = `${top.join(', ')} over ${SEASON_STRENGTH_MONTHS} months`
  if (data.seasons.length > 0) {
    const s = data.seasons[0]
    const m = (iso) => new Date(`${iso}T00:00:00`).toLocaleDateString('en-AU', { month: 'short' })
    line += ` · in-season ${m(s.start)}–${m(s.end)}`
  }
  return line
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

// Plain "averaged N (M before)" line under the recovery-vs-load chart — a
// description of what the line did, deliberately not a recommendation.
function readinessSummary(weeks) {
  const mean = (list) => {
    const vals = list.map((w) => w.meanReadiness).filter((v) => v != null)
    return vals.length ? Math.round(vals.reduce((s, v) => s + v, 0) / vals.length) : null
  }
  const recent = mean(weeks.slice(-4))
  if (recent == null) return null
  const prior = mean(weeks.slice(-8, -4))
  return prior == null
    ? `Readiness averaged ${recent} over the last 4 weeks.`
    : `Readiness averaged ${recent} over the last 4 weeks (${prior} in the 4 before).`
}

export default function TrendsPage() {
  const [months, setMonths] = useState(null)
  const [liftTrends, setLiftTrends] = useState(null)
  const [runTypeTrends, setRunTypeTrends] = useState(null)
  const [loadTrend, setLoadTrend] = useState(null)
  const [seasonStrength, setSeasonStrength] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getTrends(6).then(setMonths).catch((err) => setError(err.message))
    getLiftTrends(6).then(setLiftTrends).catch(() => setLiftTrends([]))
    getRunTypeTrends(6).then(setRunTypeTrends).catch(() => setRunTypeTrends([]))
    getLoadTrend(12).then(setLoadTrend).catch(() => setLoadTrend([]))
    getSeasonStrength(SEASON_STRENGTH_MONTHS).then(setSeasonStrength).catch(() => setSeasonStrength(null))
  }, [])

  const hasAnyData = months?.some((m) => m.gymSessions > 0 || m.runSessions > 0)
  const summary = months && hasAnyData ? periodSummary(months) : null

  const showLoadTrend = loadTrend?.some((w) => w.gymVolume > 0 || w.meanReadiness != null)
  const loadSummary = showLoadTrend ? readinessSummary(loadTrend) : null

  return (
    <main className="page">
      <h1>Trends</h1>

      {error && <p className="error">{error}</p>}
      {!error && months === null && <p>Loading trends…</p>}

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

          {seasonStrength?.series?.length >= 2 && (
            <div className="section-gap">
              <SeasonStrengthChart data={seasonStrength} />
              {seasonStrengthSummary(seasonStrength) && (
                <p className="trend-summary">{seasonStrengthSummary(seasonStrength)}</p>
              )}
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

      {showLoadTrend && (
        <div className="section-gap">
          <LoadVsWellnessChart weeks={loadTrend} />
          {loadSummary && <p className="trend-summary">{loadSummary}</p>}
        </div>
      )}

      <MuscleBalanceSection />

      {/* Bottom of the page deliberately, but kept: the season strength chart
          above is richer per lift, yet only covers program-template lifts.
          This is the only all-lifts view — every lift with history, sorted by
          movement over the window. Reviewed and kept 2026-09-02; not a
          removal candidate. */}
      {months && hasAnyData && liftTrends && liftTrends.length > 0 && (
        <div className="section-gap">
          <h2 className="trend-chart-title">Lift trends</h2>
          <LiftTrendsList trends={liftTrends} />
        </div>
      )}
    </main>
  )
}
