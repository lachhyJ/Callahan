import { useEffect, useState } from 'react'
import { getRunningSessions, getWorkoutSessions } from '../api/client'

function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

export default function HistoryPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)

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

  return (
    <main className="page">
      <h1>History</h1>
      {error && <p className="error">{error}</p>}
      {items === null && !error && <p>Loading…</p>}
      {items?.length === 0 && <p>No sessions logged yet.</p>}
      {items?.map((item) => (
        <div key={`${item.type}-${item.id}`} className="history-item">
          <strong>{item.date}</strong>{' '}
          {item.type === 'workout' ? (
            <span>{item.templateName ?? 'Workout'} — {item.setCount} set{item.setCount === 1 ? '' : 's'}</span>
          ) : (
            <span>Run — {item.distanceKm} km in {formatDuration(item.durationSeconds)}</span>
          )}
          {item.notes && <p className="notes">{item.notes}</p>}
        </div>
      ))}
    </main>
  )
}
