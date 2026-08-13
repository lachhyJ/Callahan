import { Link } from 'react-router-dom'
import { activityLabel } from '../utils/activityLabel'

function workoutLabel(w) {
  return w.templateName ?? w.categorySummary ?? 'Workout'
}

// Compact preview, deliberately lighter than History's full log entries —
// used anywhere someone's just checking "what did I do that day/week"
// (the day-detail sheet, a streak's week-by-week breakdown).
export default function SessionList({ workouts, runs, onLinkClick }) {
  return (
    <>
      {workouts.map((w) => (
        <Link key={`w-${w.id}`} to={`/sessions/${w.id}`} className="session-link" onClick={onLinkClick}>
          {workoutLabel(w)} · {w.setCount} set{w.setCount === 1 ? '' : 's'}
        </Link>
      ))}
      {runs.map((r) => (
        <p key={`r-${r.id}`}>{activityLabel(r)}</p>
      ))}
    </>
  )
}
