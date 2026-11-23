import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getClubSession, getClubSessions } from '../api/client'
import { formatDateLong } from '../dateUtils'
import { formatHoursMinutes } from '../utils/format'
import { ZONE_LABELS, zoneShares, versusUsual } from '../utils/clubSessions'

function Stat({ label, value, unit, delta }) {
  if (value == null) return null
  return (
    <div className="stat-card">
      <span className="stat-label">{label}</span>
      <span className="stat-value">{value}{unit ? ` ${unit}` : ''}</span>
      {delta != null && delta !== 0 && (
        <span className="stat-label">{delta > 0 ? '+' : ''}{delta}% vs your usual</span>
      )}
    </div>
  )
}

export default function ClubSessionPage() {
  const { sessionId } = useParams()
  const [session, setSession] = useState(null)
  const [all, setAll] = useState([])
  const [error, setError] = useState(null)

  useEffect(() => {
    setSession(null)
    setError(null)
    getClubSession(sessionId).then(setSession).catch((err) => setError(
      // 404 covers both a deleted activity and one no longer tagged Club Training.
      err.status === 404 ? 'This isn\'t a club session any more. It may have been retagged or deleted.' : err.message,
    ))
  }, [sessionId])

  // Silent: the comparison is an extra, never allowed to block the figures.
  useEffect(() => {
    getClubSessions().then(setAll).catch(() => {})
  }, [])

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
        <p><Link to="/club-sessions" className="season-link club-back-link">All club sessions</Link></p>
      </main>
    )
  }
  if (!session) return <main className="page"><p>Loading…</p></main>

  const vs = (pick) => versusUsual(all, session, pick)
  const shares = zoneShares(session.zoneSeconds)

  return (
    <main className="page">
      <p className="session-date">{[formatDateLong(session.date), formatHoursMinutes(session.durationSeconds)].join(' · ')}</p>
      <h1 className="game-title">{session.title?.trim() || 'Club Training'}</h1>

      <div className="stat-grid">
        <Stat label="Distance" value={session.distanceKm == null ? null : Number(session.distanceKm).toFixed(1)} unit="km" delta={vs((s) => s.distanceKm)} />
        <Stat label="Avg HR" value={session.avgHeartRate} unit="bpm" />
        <Stat label="Max HR" value={session.maxHeartRate} unit="bpm" />
        <Stat label="Calories" value={session.calories} />
        <Stat label="Training load" value={session.trainingLoad == null ? null : Math.round(session.trainingLoad)} delta={vs((s) => s.trainingLoad)} />
        <Stat label="Training effect" value={session.aerobicTrainingEffect == null ? null : `${session.aerobicTrainingEffect} / ${session.anaerobicTrainingEffect ?? '–'}`} />
        <Stat label="High-speed distance" value={session.highSpeedDistanceM == null ? null : Math.round(session.highSpeedDistanceM)} unit="m" delta={vs((s) => s.highSpeedDistanceM)} />
        <Stat label="Sprints" value={session.sprintCount} delta={vs((s) => s.sprintCount)} />
      </div>

      {shares && (
        <section>
          <h2 className="trend-chart-title">Time in heart-rate zones</h2>
          <div className="club-zone-bar" role="img" aria-label="Share of time in each heart-rate zone">
            {shares.map((z, i) => (
              <div key={i} className={`club-zone club-zone-${i + 1}`} style={{ width: `${z.pct}%` }} />
            ))}
          </div>
          <ul className="club-zone-legend">
            {shares.map((z, i) => (
              <li key={i}><i className={`club-zone-swatch club-zone-${i + 1}`} />{ZONE_LABELS[i]} {formatHoursMinutes(z.sec)} · {Math.round(z.pct)}%</li>
            ))}
          </ul>
        </section>
      )}

      <p className="game-method-note">
        Distance is the whole recording, warm-up and rests included, since a club session is one
        continuous activity. High-speed distance and sprints count movement above 5.5 m/s in the
        GPS track; the threshold is provisional.
        {session.sprintCount == null && ' No GPS track has synced for this session yet.'}
      </p>
      <p><Link to="/club-sessions" className="season-link club-back-link">All club sessions</Link></p>
    </main>
  )
}
