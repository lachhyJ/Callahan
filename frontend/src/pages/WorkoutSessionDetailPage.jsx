import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { deleteWorkoutSession, getWorkoutSession, restoreWorkoutSession, updateWorkoutSessionName } from '../api/client'
import { workoutLabel } from '../components/SessionList'
import { formatDateLong } from '../dateUtils'
import { SET_TYPE_LABELS, formatWeight } from '../utils/format'

const UNDO_WINDOW_MS = 6000



function formatDuration(startedAt, finishedAt) {
  if (!startedAt || !finishedAt) return null
  const ms = new Date(finishedAt) - new Date(startedAt)
  const minutes = Math.round(ms / 60000)
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return hours > 0 ? `${hours}h ${mins}min` : `${mins}min`
}

// Groups sets by exercise while preserving first-appearance order — sets come
// back ordered by SetOrder within an exercise, not grouped, so this rebuilds
// the same "exercise card" shape the active workout page uses.
function groupByExercise(sets) {
  const order = []
  const map = new Map()
  for (const s of sets) {
    if (!map.has(s.exerciseId)) {
      map.set(s.exerciseId, { exerciseId: s.exerciseId, exerciseName: s.exerciseName, sets: [] })
      order.push(s.exerciseId)
    }
    map.get(s.exerciseId).sets.push(s)
  }
  return order.map((id) => map.get(id))
}

export default function WorkoutSessionDetailPage() {
  const { sessionId } = useParams()
  const navigate = useNavigate()
  const [session, setSession] = useState(null)
  const [error, setError] = useState(null)
  const [deleted, setDeleted] = useState(false)
  const [restoring, setRestoring] = useState(false)

  useEffect(() => {
    setSession(null)
    setError(null)
    setDeleted(false)
    setRestoring(false)
    getWorkoutSession(sessionId).then(setSession).catch((err) => setError(err.message))
  }, [sessionId])

  useEffect(() => {
    if (!deleted) return
    const timeout = setTimeout(() => navigate('/history'), UNDO_WINDOW_MS)
    return () => clearTimeout(timeout)
  }, [deleted, navigate])

  function updateNameLocal(value) {
    setSession((prev) => ({ ...prev, name: value }))
  }

  function handleNameBlur(value) {
    updateWorkoutSessionName(sessionId, value.trim() || null).catch(() => {})
  }

  function handleDelete() {
    if (!window.confirm('Delete this session? You can restore it from Recently Deleted within 7 days.')) return
    deleteWorkoutSession(sessionId).then(() => setDeleted(true)).catch((err) => setError(err.message))
  }

  function handleUndo() {
    if (restoring) return
    setRestoring(true)
    restoreWorkoutSession(sessionId)
      .then(() => setDeleted(false))
      .catch((err) => setError(err.message))
      .finally(() => setRestoring(false))
  }

  if (deleted) {
    return (
      <main className="page">
        <p className="delete-toast">
          Session deleted. <button type="button" disabled={restoring} onClick={handleUndo}>{restoring ? 'Undoing…' : 'Undo'}</button>
        </p>
      </main>
    )
  }

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!session) {
    return (
      <main className="page">
        <p>Loading session…</p>
      </main>
    )
  }

  const duration = formatDuration(session.startedAt, session.finishedAt)
  const exercises = groupByExercise(session.sets)
  const notesByExercise = new Map(session.exerciseNotes.map((n) => [n.exerciseId, n.notes]))

  return (
    <main className="page">
      <input
        type="text"
        className="session-name-input"
        placeholder={workoutLabel(session)}
        value={session.name ?? ''}
        onChange={(e) => updateNameLocal(e.target.value)}
        onBlur={(e) => handleNameBlur(e.target.value)}
        aria-label="Session name"
      />
      <p className="session-date">{formatDateLong(session.date)}</p>
      {duration && <p className="session-duration">{duration} · {session.sets.length} set{session.sets.length === 1 ? '' : 's'}</p>}
      {session.notes && <p className="notes">{session.notes}</p>}

      {exercises.length === 0 && (
        <div className="empty-state">
          <p>No sets logged in this session.</p>
        </div>
      )}

      <div className="exercise-history-section">
        {exercises.map((ex) => (
          <div key={ex.exerciseId} className="history-entry">
            <strong>{ex.exerciseName}</strong>
            {notesByExercise.has(ex.exerciseId) && <p className="notes">{notesByExercise.get(ex.exerciseId)}</p>}
            <ul className="history-set-list">
              {ex.sets.map((s) => (
                <li key={s.id}>
                  <span className="history-set-number">Set {s.setOrder + 1}</span>
                  <span className={`history-set-type set-type-${s.setType.toLowerCase()}`}>{SET_TYPE_LABELS[s.setType]}</span>
                  <span>{s.reps} × {formatWeight(s.weightKg)} kg</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <button type="button" className="discard-btn" onClick={handleDelete}>Delete session</button>
    </main>
  )
}
