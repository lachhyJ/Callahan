import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { getActivities, getWorkoutSessions } from '../api/client'
import { CheckIcon } from '../icons'
import { activityLabel } from '../utils/activityLabel'

export default function HistoryPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const location = useLocation()
  const [savedMessage, setSavedMessage] = useState(location.state?.savedMessage ?? null)

  useEffect(() => {
    Promise.all([getWorkoutSessions(), getActivities()])
      .then(([workouts, activities]) => {
        const merged = [
          ...workouts.map((w) => ({ kind: 'workout', ...w })),
          ...activities.map((a) => ({ kind: 'activity', ...a })),
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
        <div key={`${item.kind}-${item.id}`} className="history-item">
          <strong>{item.date}</strong>{' '}
          {item.kind === 'workout' ? (
            <Link to={`/sessions/${item.id}`} className="session-link">
              {item.templateName ?? item.categorySummary ?? 'Workout'} — {item.setCount} set{item.setCount === 1 ? '' : 's'}
            </Link>
          ) : (
            <span>{activityLabel(item)}</span>
          )}
          {item.notes && <p className="notes">{item.notes}</p>}
        </div>
      ))}
    </main>
  )
}
