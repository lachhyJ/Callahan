import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { getWorkoutSession } from '../api/client'
import { BackIcon } from '../icons'

const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }

function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

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

  useEffect(() => {
    setSession(null)
    setError(null)
    getWorkoutSession(sessionId).then(setSession).catch((err) => setError(err.message))
  }, [sessionId])

  if (error) {
    return (
      <main className="page">
        <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!session) {
    return (
      <main className="page">
        <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
        <p>Loading…</p>
      </main>
    )
  }

  const duration = formatDuration(session.startedAt, session.finishedAt)
  const exercises = groupByExercise(session.sets)
  const notesByExercise = new Map(session.exerciseNotes.map((n) => [n.exerciseId, n.notes]))

  return (
    <main className="page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>{session.date}</h1>
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
    </main>
  )
}
