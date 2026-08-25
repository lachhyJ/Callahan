import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useActivityClassification } from '../hooks/useActivityClassification'
import ActivitySessionRow from './ActivitySessionRow'

export function workoutLabel(w) {
  if (w.name) return w.name
  if (w.templateName) return w.templateSubtitle ? `${w.templateName} — ${w.templateSubtitle}` : w.templateName
  return w.categorySummary ?? 'Workout'
}

// Compact preview, deliberately lighter than History's full log entries —
// used anywhere someone's just checking "what did I do that day/week"
// (the day-detail sheet, a streak's week-by-week breakdown). Runs and
// Ultimate activities are classifiable here too, via the same picker as
// History, so this doesn't become a second, weaker place to see an activity
// that can't actually be tagged.
export default function SessionList({ workouts, runs, onLinkClick }) {
  const [overrides, setOverrides] = useState({})
  const { sessionTypes, openPickerId, togglePicker, selectSessionType, setConeDistance } = useActivityClassification(
    (updated) => setOverrides((current) => ({ ...current, [updated.id]: updated }))
  )

  return (
    <>
      {workouts.map((w) => (
        <Link key={`w-${w.id}`} to={`/sessions/${w.id}`} className="session-link" onClick={onLinkClick}>
          {workoutLabel(w)} · {w.setCount} set{w.setCount === 1 ? '' : 's'}
        </Link>
      ))}
      {runs.map((r) => {
        const activity = overrides[r.id] ?? r
        return (
          <ActivitySessionRow
            key={`r-${r.id}`}
            activity={activity}
            sessionTypes={sessionTypes}
            openPickerId={openPickerId}
            onTogglePicker={togglePicker}
            onSelect={selectSessionType}
            onConeDistanceChange={setConeDistance}
          />
        )
      })}
    </>
  )
}
