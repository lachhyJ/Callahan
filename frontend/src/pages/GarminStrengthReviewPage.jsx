import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { dismissPendingGarminStrength, getPendingGarminStrength, getWorkoutSessions, linkPendingGarminStrength } from '../api/client'
import { workoutLabel } from '../utils/workoutLabel'
import { formatDateLong, formatTimeOfDay } from '../dateUtils'
import { formatSessionDuration } from '../utils/format'


export default function GarminStrengthReviewPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const [busyKey, setBusyKey] = useState(null)
  // Every session, fetched once - the auto-match candidates are same-day
  // only, but the right session for an activity Garmin misdated (or a day
  // Callahan has no session logged for) can be anywhere. Fetched lazily
  // (only once a search box is actually opened) since most pending items
  // resolve from their same-day candidates without ever needing it.
  const [allSessions, setAllSessions] = useState(null)
  const [searchOpenId, setSearchOpenId] = useState(null)
  const [searchQuery, setSearchQuery] = useState('')

  useEffect(() => {
    getPendingGarminStrength().then(setItems).catch((err) => setError(err.message))
  }, [])

  function toggleSearch(pendingId) {
    if (searchOpenId === pendingId) {
      setSearchOpenId(null)
      return
    }
    setSearchOpenId(pendingId)
    setSearchQuery('')
    if (allSessions === null) {
      getWorkoutSessions().then(setAllSessions).catch((err) => setError(err.message))
    }
  }

  async function handleLink(pendingId, sessionId) {
    const key = `link-${pendingId}`
    if (busyKey) return
    setBusyKey(key)
    try {
      await linkPendingGarminStrength(pendingId, sessionId)
      setItems((current) => current.filter((i) => i.id !== pendingId))
      setSearchOpenId(null)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusyKey(null)
    }
  }

  async function handleDismiss(pendingId) {
    const key = `dismiss-${pendingId}`
    if (busyKey) return
    if (!window.confirm("Dismiss this Garmin activity? It won't be linked to any session.")) return
    setBusyKey(key)
    try {
      await dismissPendingGarminStrength(pendingId)
      setItems((current) => current.filter((i) => i.id !== pendingId))
    } catch (err) {
      setError(err.message)
    } finally {
      setBusyKey(null)
    }
  }

  return (
    <main className="page">
      <h1>Garmin strength activities to review</h1>
      <p className="notes">
        These Garmin-logged lifting sessions couldn't be matched to exactly one gym session automatically —
        pick the one each belongs to, or dismiss it if it isn't logged in Callahan.
      </p>

      {error && <p className="error">{error}</p>}
      {items === null && !error && <p>Loading…</p>}

      {items && items.length === 0 && (
        <div className="empty-state">
          <p>Nothing to review right now.</p>
        </div>
      )}

      {items && items.map((item) => (
        <div key={item.id} className="history-item garmin-strength-review-item">
          <div className="history-item-row">
            <span className="history-item-main">
              <strong>{formatDateLong(item.date)}</strong>{' '}
              {item.startedAt && <span>started {formatTimeOfDay(item.startedAt)}</span>}
              <p className="notes">
                {formatSessionDuration(item.durationSeconds)}
                {item.calories != null && ` · ${item.calories} cal`}
                {item.avgHeartRate != null && ` · ${item.avgHeartRate} bpm avg`}
                {item.notes && ` · "${item.notes}"`}
              </p>
            </span>
            <button
              type="button"
              className="secondary-btn"
              disabled={busyKey !== null}
              onClick={() => handleDismiss(item.id)}
            >
              {busyKey === `dismiss-${item.id}` ? 'Dismissing…' : 'Dismiss'}
            </button>
          </div>

          {item.candidates.length === 0 ? (
            <p className="notes">No gym sessions logged on this date to link to.</p>
          ) : (
            <ul className="garmin-strength-candidate-list">
              {item.candidates.map((c) => (
                <li key={c.sessionId}>
                  <span>
                    <Link to={`/sessions/${c.sessionId}`}>{workoutLabel(c)}</Link>
                    {c.startedAt && ` · started ${formatTimeOfDay(c.startedAt)}`}
                  </span>
                  <button
                    type="button"
                    className="secondary-btn"
                    disabled={busyKey !== null}
                    onClick={() => handleLink(item.id, c.sessionId)}
                  >
                    {busyKey === `link-${item.id}` ? 'Linking…' : 'Link'}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <button
            type="button"
            className="secondary-btn garmin-strength-search-toggle"
            onClick={() => toggleSearch(item.id)}
          >
            {searchOpenId === item.id ? 'Cancel' : 'Link to a different session'}
          </button>

          {searchOpenId === item.id && (
            <div className="garmin-strength-search">
              <input
                type="text"
                placeholder="Search by date or name…"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                autoFocus
              />
              {allSessions === null && <p className="notes">Loading sessions…</p>}
              {allSessions && (
                <ul className="garmin-strength-candidate-list">
                  {allSessions
                    .filter((s) => {
                      const q = searchQuery.trim().toLowerCase()
                      if (!q) return true
                      return workoutLabel(s).toLowerCase().includes(q) || formatDateLong(s.date).toLowerCase().includes(q)
                    })
                    .slice(0, 20)
                    .map((s) => (
                      <li key={s.id}>
                        <span>
                          <Link to={`/sessions/${s.id}`}>{workoutLabel(s)}</Link>
                          {` · ${formatDateLong(s.date)}`}
                          {s.startedAt && ` · started ${formatTimeOfDay(s.startedAt)}`}
                        </span>
                        <button
                          type="button"
                          className="secondary-btn"
                          disabled={busyKey !== null}
                          onClick={() => handleLink(item.id, s.id)}
                        >
                          {busyKey === `link-${item.id}` ? 'Linking…' : 'Link'}
                        </button>
                      </li>
                    ))}
                </ul>
              )}
            </div>
          )}
        </div>
      ))}
    </main>
  )
}
