import { useEffect, useState } from 'react'
import {
  getDeletedActivities,
  getDeletedWorkoutSessions,
  restoreActivity,
  restoreWorkoutSession,
} from '../api/client'
import { activityLabel } from '../utils/activityLabel'
import { workoutLabel } from '../components/SessionList'

const RECOVERY_WINDOW_DAYS = 7

function daysRemaining(deletedAt) {
  const elapsedMs = Date.now() - new Date(deletedAt).getTime()
  const elapsedDays = Math.floor(elapsedMs / 86400000)
  return Math.max(0, RECOVERY_WINDOW_DAYS - elapsedDays)
}

export default function RecentlyDeletedPage() {
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    Promise.all([getDeletedWorkoutSessions(), getDeletedActivities()])
      .then(([workouts, activities]) => {
        const merged = [
          ...workouts.map((w) => ({ kind: 'workout', ...w })),
          ...activities.map((a) => ({ kind: 'activity', ...a })),
        ].sort((a, b) => b.deletedAt.localeCompare(a.deletedAt))
        setItems(merged)
      })
      .catch((err) => setError(err.message))
  }, [])

  async function handleRestore(item) {
    try {
      if (item.kind === 'workout') await restoreWorkoutSession(item.id)
      else await restoreActivity(item.id)
      setItems((current) => current.filter((i) => !(i.kind === item.kind && i.id === item.id)))
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <main className="page">
      <h1>Recently deleted</h1>
      {error && <p className="error">{error}</p>}
      {items === null && !error && <p>Loading…</p>}

      {items && items.length === 0 && (
        <div className="empty-state">
          <p>Nothing in here — anything you delete shows up here for 7 days before it's gone for good.</p>
        </div>
      )}

      {items && items.length > 0 && (
        <div className="recently-deleted-list">
          {items.map((item) => (
            <div key={`${item.kind}-${item.id}`} className="history-item">
              <div className="history-item-row">
                <span className="history-item-main">
                  <strong>{item.date}</strong>{' '}
                  <span>{item.kind === 'workout' ? workoutLabel(item) : activityLabel(item)}</span>
                  <p className="notes">
                    {daysRemaining(item.deletedAt) === 0
                      ? 'Purged shortly'
                      : `${daysRemaining(item.deletedAt)} day${daysRemaining(item.deletedAt) === 1 ? '' : 's'} left to restore`}
                  </p>
                </span>
                <button type="button" className="secondary-btn" onClick={() => handleRestore(item)}>Restore</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </main>
  )
}
