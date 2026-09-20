import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { dismissPendingGarminStrength, getPendingGarminStrength, linkPendingGarminStrength } from '../api/client'
import { workoutLabel } from '../components/SessionList'
import { formatDateLong } from '../dateUtils'

function formatClock(dateTime) {
  if (!dateTime) return null
  return new Date(dateTime).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
}

function formatDuration(seconds) {
  const minutes = Math.round(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return hours > 0 ? `${hours}h ${mins}min` : `${mins}min`
}

export default function GarminStrengthReviewPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const [busyKey, setBusyKey] = useState(null)

  useEffect(() => {
    getPendingGarminStrength().then(setItems).catch((err) => setError(err.message))
  }, [])

  async function handleLink(pendingId, sessionId) {
    const key = `link-${pendingId}`
    if (busyKey) return
    setBusyKey(key)
    try {
      await linkPendingGarminStrength(pendingId, sessionId)
      setItems((current) => current.filter((i) => i.id !== pendingId))
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
              {item.startedAt && <span>started {formatClock(item.startedAt)}</span>}
              <p className="notes">
                {formatDuration(item.durationSeconds)}
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
                    {c.startedAt && ` · started ${formatClock(c.startedAt)}`}
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
        </div>
      ))}
    </main>
  )
}
