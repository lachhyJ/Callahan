import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { getActivities, getWorkoutSessions } from '../api/client'
import { CheckIcon } from '../icons'
import { activityLabel } from '../utils/activityLabel'
import { workoutLabel } from '../components/SessionList'
import { isoDate, startOfWeek } from '../dateUtils'

function formatWeekLabel(weekStartIso) {
  const start = new Date(`${weekStartIso}T00:00:00`)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`
}

// Groups the flat, already-sorted item list into Monday-first weeks —
// same convention as the Calendar grid and Streaks pages.
function groupByWeek(items) {
  const weeks = []
  let current = null
  for (const item of items) {
    const weekStart = isoDate(startOfWeek(new Date(`${item.date}T00:00:00`)))
    if (!current || current.weekStart !== weekStart) {
      current = { weekStart, items: [] }
      weeks.push(current)
    }
    current.items.push(item)
  }
  return weeks
}

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

  const weeks = items ? groupByWeek(items) : []

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
      {weeks.length > 0 && (
        <div className="history-week-list">
          {weeks.map((week) => (
            <div key={week.weekStart} className="history-week">
              <div className="history-week-header">
                <span>{formatWeekLabel(week.weekStart)}</span>
                <span className="history-week-count">
                  {week.items.length} session{week.items.length === 1 ? '' : 's'}
                </span>
              </div>
              {week.items.map((item) => (
                <div key={`${item.kind}-${item.id}`} className="history-item">
                  <strong>{item.date}</strong>{' '}
                  {item.kind === 'workout' ? (
                    <Link to={`/sessions/${item.id}`} className="session-link">
                      {workoutLabel(item)} · {item.setCount} set{item.setCount === 1 ? '' : 's'}
                    </Link>
                  ) : (
                    <span>{activityLabel(item)}</span>
                  )}
                  {item.notes && <p className="notes">{item.notes}</p>}
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </main>
  )
}
