import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getWorkoutTemplates } from '../api/client'

export default function WorkoutTemplatesPage() {
  const [templates, setTemplates] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getWorkoutTemplates()
      .then(setTemplates)
      .catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      <h1>Start a workout</h1>
      {error && <p className="error">{error}</p>}
      {templates === null && !error && <p>Loading…</p>}
      <div className="template-list">
        {templates?.map((t) => (
          <Link key={t.id} to={`/workout/${t.id}`} className="template-card">
            {t.name}
          </Link>
        ))}
      </div>
      <Link to="/workout/custom" className="custom-workout-link">
        Or start an empty workout
      </Link>
    </main>
  )
}
