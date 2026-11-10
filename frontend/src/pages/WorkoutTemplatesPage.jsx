import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { getWorkoutTemplates } from '../api/client'
import { loadActiveWorkout } from '../activeWorkout'
import { unlockAudio } from '../audio'
import { trackAction } from '../usage'
import { staleWhileRevalidate } from '../swrCache'
import { RunIcon } from '../icons'
import TrainingModeSetting from '../components/TrainingModeSetting'

// "Due": this session's slot was planned for an earlier day this week and is not logged yet
// (the server decides, same rule as the This week widget). The pill is positioned out of
// flow so showing or clearing it never moves the card or the ones below it.
export function TemplateCard({ t }) {
  return (
    <Link to={`/workout/${t.id}`} className="template-card" onClick={() => { unlockAudio(); trackAction('start-workout', t.name) }}>
      {t.name}
      {t.due && <span className="template-card-due">Due</span>}
      <span className="template-card-subtitle">{t.subtitle}</span>
    </Link>
  )
}

export default function WorkoutTemplatesPage() {
  const [templates, setTemplates] = useState(null)
  const [error, setError] = useState(null)
  const [activeWorkout] = useState(() => loadActiveWorkout())

  const loadTemplates = useCallback(
    () => staleWhileRevalidate('workouttemplates', getWorkoutTemplates, setTemplates).catch((err) => setError(err.message)),
    [],
  )

  useEffect(() => {
    if (activeWorkout) return
    loadTemplates()
  }, [activeWorkout, loadTemplates])

  if (activeWorkout) {
    return <Navigate to={`/workout/${activeWorkout.templateId}`} replace />
  }

  return (
    <main className="page">
      <h1>Start a workout</h1>
      <TrainingModeSetting onChange={() => { setTemplates(null); loadTemplates() }} />
      {error && <p className="error">{error}</p>}
      {templates === null && !error && <p>Loading workouts…</p>}
      <div className="template-list">
        {templates?.map((t) => (
          <TemplateCard key={t.id} t={t} />
        ))}
      </div>
      <Link to="/workout/custom" className="custom-workout-link" onClick={() => { unlockAudio(); trackAction('start-workout', 'custom') }}>
        Or start an empty workout
      </Link>
      <Link to="/run" className="log-run-card" onClick={() => trackAction('log-activity')}>
        <RunIcon /> Log an activity
      </Link>
    </main>
  )
}
