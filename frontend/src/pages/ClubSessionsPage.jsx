import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getClubSessions } from '../api/client'
import { formatDateMedium } from '../dateUtils'
import { formatHoursMinutes } from '../utils/format'
import { CLUB_COLUMNS } from '../utils/clubSessions'

// Every Club Training recording, newest first, with the headline figures in
// columns so sessions can be compared by scanning down.
export default function ClubSessionsPage() {
  const [sessions, setSessions] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getClubSessions().then(setSessions).catch((err) => setError(err.message))
  }, [])

  if (error) return <main className="page"><p className="error">{error}</p></main>
  if (!sessions) return <main className="page"><p>Loading…</p></main>

  return (
    <main className="page">
      <h1 className="game-title">Club sessions</h1>
      {sessions.length === 0 ? (
        <p className="game-method-note">No club sessions yet. Sessions tagged Club Training show up here.</p>
      ) : (
        <div className="club-table" role="table" aria-label="Club sessions">
          <div className="club-row club-row-head" role="row">
            <span role="columnheader">Session</span>
            {CLUB_COLUMNS.map((c) => <span key={c.key} role="columnheader" className="club-num">{c.label}</span>)}
          </div>
          {sessions.map((s) => (
            <Link key={s.id} to={`/club-sessions/${s.id}`} className="club-row" role="row">
              <span className="club-session-cell" role="cell">
                {formatDateMedium(s.date)}
                <span className="club-session-sub">{formatHoursMinutes(s.durationSeconds)}</span>
              </span>
              {CLUB_COLUMNS.map((c) => {
                const v = c.value(s)
                return <span key={c.key} role="cell" className="club-num">{v ?? '–'}</span>
              })}
            </Link>
          ))}
        </div>
      )}
      <p className="game-method-note">
        Sprints are efforts above 5.5 m/s from the GPS track. The threshold is provisional, so
        compare sessions with each other rather than reading the count as exact.
      </p>
    </main>
  )
}
