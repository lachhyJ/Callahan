import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { getRunningSessions, getWorkoutSessions } from '../api/client'
import { CheckIcon } from '../icons'

function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

export default function HistoryPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const location = useLocation()
  const [savedMessage, setSavedMessage] = useState(location.state?.savedMessage ?? null)

  useEffect(() => {
    Promise.all([getWorkoutSessions(), getRunningSessions()])
      .then(([workouts, runs]) => {
        const merged = [
          ...workouts.map((w) => ({ type: 'workout', ...w })),
          ...runs.map((r) => ({ type: 'run', ...r })),
        ].sort((a, b) => b.date.localeCompare(a.date))
        setItems(merged)
      })
      .catch((err) => setError(err.message))
  }, [])

  useEffect(() => {
    if (!savedMessage) return
    const timeout = setTimeout(() => setSavedMessage(null), 4000)
    return () => clearTimeout(timeout)
  }, [savedMessage])

  return (
    <main className="page">
      <h1>History</h1>
      {savedMessage && (
        <p className="save-confirmation"><CheckIcon /> {savedMessage}</p>
      )}
      {error && <p className="error">{error}</p>}
      {items === null && !error && <p>Loading…</p>}
      {items?.length === 0 && (
        <div className="empty-state">
          <p>No sessions logged yet.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}
      {items?.map((item) => (
        <div key={`${item.type}-${item.id}`} className="history-item">
          <strong>{item.date}</strong>{' '}
          {item.type === 'workout' ? (
            <Link to={`/sessions/${item.id}`} className="session-link">
              {item.templateName ?? 'Workout'} — {item.setCount} set{item.setCount === 1 ? '' : 's'}
            </Link>
          ) : (
            <span>Run — {item.distanceKm} km in {formatDuration(item.durationSeconds)}</span>
          )}
          {item.notes && <p className="notes">{item.notes}</p>}
        </div>
      ))}
    </main>
  )
}
