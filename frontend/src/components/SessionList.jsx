import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { activityLabel } from '../utils/activityLabel'
import { getRunSessionTypes, updateActivityRunSessionType } from '../api/client'
import RunActivityRow from './RunActivityRow'

export function workoutLabel(w) {
  if (w.name) return w.name
  if (w.templateName) return w.templateSubtitle ? `${w.templateName} — ${w.templateSubtitle}` : w.templateName
  return w.categorySummary ?? 'Workout'
}

// Compact preview, deliberately lighter than History's full log entries —
// used anywhere someone's just checking "what did I do that day/week"
// (the day-detail sheet, a streak's week-by-week breakdown). Runs are
// classifiable here too, via the same picker as History, so this doesn't
// become a second, weaker place to see a run that can't actually be tagged.
export default function SessionList({ workouts, runs, onLinkClick }) {
  const [runSessionTypes, setRunSessionTypes] = useState([])
  const [openPickerId, setOpenPickerId] = useState(null)
  const [overrides, setOverrides] = useState({})

  useEffect(() => {
    getRunSessionTypes().then(setRunSessionTypes).catch(() => {})
  }, [])

  function togglePicker(activityId) {
    setOpenPickerId((current) => (current === activityId ? null : activityId))
  }

  async function selectRunSessionType(activityId, runSessionTypeId) {
    setOpenPickerId(null)
    const updated = await updateActivityRunSessionType(activityId, runSessionTypeId)
    setOverrides((current) => ({ ...current, [activityId]: updated }))
  }

  return (
    <>
      {workouts.map((w) => (
        <Link key={`w-${w.id}`} to={`/sessions/${w.id}`} className="session-link" onClick={onLinkClick}>
          {workoutLabel(w)} · {w.setCount} set{w.setCount === 1 ? '' : 's'}
        </Link>
      ))}
      {runs.map((r) => {
        const activity = overrides[r.id] ?? r
        return activity.type === 'Running' ? (
          <RunActivityRow
            key={`r-${r.id}`}
            activity={activity}
            runSessionTypes={runSessionTypes}
            openPickerId={openPickerId}
            onTogglePicker={togglePicker}
            onSelect={selectRunSessionType}
          />
        ) : (
          <p key={`r-${r.id}`}>{activityLabel(activity)}</p>
        )
      })}
    </>
  )
}
