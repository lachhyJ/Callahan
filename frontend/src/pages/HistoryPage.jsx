import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getActivities, getWorkoutSessions } from '../api/client'
import { activityLabel } from '../utils/activityLabel'
import { workoutLabel } from '../components/SessionList'
import { isoDate, startOfWeek } from '../dateUtils'

function formatWeekLabel(weekStartIso) {
  const start = new Date(`${weekStartIso}T00:00:00`)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`
}

// Groups the flat, already-sorted item list into Monday-first weeks — same
// convention as the Calendar grid and Streaks pages — and fills in any gap
// weeks (no workouts, no runs) between the earliest logged week and the
// most recent of "today" or the latest logged week, so a dry spell shows
// up as a run of empty weeks rather than just vanishing from the list.
function groupByWeek(items) {
  if (items.length === 0) return []

  const byWeekStart = new Map()
  for (const item of items) {
    const weekStart = isoDate(startOfWeek(new Date(`${item.date}T00:00:00`)))
    if (!byWeekStart.has(weekStart)) byWeekStart.set(weekStart, [])
    byWeekStart.get(weekStart).push(item)
  }

  const weekStarts = [...byWeekStart.keys()].sort()
  const earliest = new Date(`${weekStarts[0]}T00:00:00`)
  const latestLogged = new Date(`${weekStarts[weekStarts.length - 1]}T00:00:00`)
  const currentWeekStart = startOfWeek(new Date())
  const latest = currentWeekStart > latestLogged ? currentWeekStart : latestLogged

  const weeks = []
  for (const d = new Date(latest); d >= earliest; d.setDate(d.getDate() - 7)) {
    const weekStart = isoDate(d)
    weeks.push({ weekStart, items: byWeekStart.get(weekStart) ?? [] })
  }
  return weeks
}

export default function HistoryPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)

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

  const weeks = items ? groupByWeek(items) : []

  return (
    <main className="page">
      <h1>History</h1>
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
            <div key={week.weekStart} className={week.items.length === 0 ? 'history-week empty' : 'history-week'}>
              <div className="history-week-header">
                <span>{formatWeekLabel(week.weekStart)}</span>
                {week.items.length > 0 && (
                  <span className="history-week-count">
                    {week.items.length} session{week.items.length === 1 ? '' : 's'}
                  </span>
                )}
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
