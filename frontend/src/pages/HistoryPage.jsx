import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { getActivities, getRunSessionTypes, getWorkoutSessions, updateActivityRunSessionType } from '../api/client'
import { activityLabel } from '../utils/activityLabel'
import { workoutLabel } from '../components/SessionList'
import { isoDate, startOfWeek } from '../dateUtils'
import { BackIcon } from '../icons'

function formatWeekLabel(weekStartIso) {
  const start = new Date(`${weekStartIso}T00:00:00`)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`
}

// Groups the flat, already-sorted item list into Monday-first weeks — same
// convention as the Calendar grid and Streaks pages — and fills in any gap
// weeks (no workouts, no runs) between the earliest logged week and the
// most recent of "today", the latest logged week, or a target week the
// caller wants guaranteed to appear (e.g. jumping in from the Dashboard
// grid to a week outside the normal range), so a dry spell shows up as a
// run of empty weeks rather than just vanishing from the list.
function groupByWeek(items, targetWeekStart) {
  if (items.length === 0) return []

  const byWeekStart = new Map()
  for (const item of items) {
    const weekStart = isoDate(startOfWeek(new Date(`${item.date}T00:00:00`)))
    if (!byWeekStart.has(weekStart)) byWeekStart.set(weekStart, [])
    byWeekStart.get(weekStart).push(item)
  }

  const weekStarts = [...byWeekStart.keys()].sort()
  let earliest = new Date(`${weekStarts[0]}T00:00:00`)
  let latest = new Date(`${weekStarts[weekStarts.length - 1]}T00:00:00`)

  const currentWeekStart = startOfWeek(new Date())
  if (currentWeekStart > latest) latest = currentWeekStart

  if (targetWeekStart) {
    const target = new Date(`${targetWeekStart}T00:00:00`)
    if (target < earliest) earliest = target
    if (target > latest) latest = target
  }

  const weeks = []
  for (const d = new Date(latest); d >= earliest; d.setDate(d.getDate() - 7)) {
    const weekStart = isoDate(d)
    weeks.push({ weekStart, items: byWeekStart.get(weekStart) ?? [] })
  }
  return weeks
}

// Runs need classifying well after the fact (mostly Garmin syncs reviewed
// during a later History browse, not right after logging), so the picker
// lives inline here rather than on the log-run flow. Unclassified
// Garmin-sourced runs get a badge so they don't quietly go unclassified.
function RunActivityRow({ activity, runSessionTypes, openPickerId, onTogglePicker, onSelect }) {
  const pickerOpen = openPickerId === activity.id
  const needsClassification = activity.source === 'Garmin' && !activity.runSessionTypeId

  return (
    <span className="run-classify">
      <button type="button" className="run-classify-trigger" onClick={() => onTogglePicker(activity.id)}>
        {activityLabel(activity)}
      </button>
      {needsClassification && <span className="needs-classification-badge">Needs classification</span>}
      {pickerOpen && (
        <div className="set-type-menu run-type-menu">
          {runSessionTypes.map((t) => (
            <button key={t.id} type="button" onClick={() => onSelect(activity.id, t.id)}>
              {t.name}
            </button>
          ))}
          {activity.runSessionTypeId && (
            <button type="button" className="remove-option" onClick={() => onSelect(activity.id, null)}>
              Clear
            </button>
          )}
        </div>
      )}
    </span>
  )
}

export default function HistoryPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const [searchParams] = useSearchParams()
  const targetWeek = searchParams.get('week')
  const targetRef = useRef(null)
  const [runSessionTypes, setRunSessionTypes] = useState([])
  const [openPickerId, setOpenPickerId] = useState(null)

  useEffect(() => {
    Promise.all([getWorkoutSessions(), getActivities(), getRunSessionTypes()])
      .then(([workouts, activities, types]) => {
        const merged = [
          ...workouts.map((w) => ({ kind: 'workout', ...w })),
          ...activities.map((a) => ({ kind: 'activity', ...a })),
        ].sort((a, b) => b.date.localeCompare(a.date))
        setItems(merged)
        setRunSessionTypes(types)
      })
      .catch((err) => setError(err.message))
  }, [])

  const weeks = items ? groupByWeek(items, targetWeek) : []

  useEffect(() => {
    if (targetWeek && targetRef.current) {
      targetRef.current.scrollIntoView({ block: 'center' })
    }
  }, [targetWeek, items])

  function togglePicker(activityId) {
    setOpenPickerId((current) => (current === activityId ? null : activityId))
  }

  async function selectRunSessionType(activityId, runSessionTypeId) {
    setOpenPickerId(null)
    const updated = await updateActivityRunSessionType(activityId, runSessionTypeId)
    setItems((current) => current.map((item) => (item.kind === 'activity' && item.id === activityId ? { ...item, ...updated } : item)))
  }

  return (
    <main className="page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
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
          {weeks.map((week) => {
            const isTarget = week.weekStart === targetWeek
            const className = [
              'history-week',
              week.items.length === 0 ? 'empty' : null,
              isTarget ? 'target' : null,
            ].filter(Boolean).join(' ')
            return (
              <div key={week.weekStart} ref={isTarget ? targetRef : null} className={className}>
                <div className="history-week-header">
                  <span>{formatWeekLabel(week.weekStart)}</span>
                  {week.items.length > 0 && (
                    <span className="history-week-count">
                      {week.items.length} session{week.items.length === 1 ? '' : 's'}
                    </span>
                  )}
                </div>
                {week.items.length === 0 && isTarget && (
                  <p className="streak-week-empty">Nothing logged</p>
                )}
                {week.items.map((item) => (
                  <div key={`${item.kind}-${item.id}`} className="history-item">
                    <strong>{item.date}</strong>{' '}
                    {item.kind === 'workout' ? (
                      <Link to={`/sessions/${item.id}`} className="session-link">
                        {workoutLabel(item)} · {item.setCount} set{item.setCount === 1 ? '' : 's'}
                      </Link>
                    ) : item.type === 'Running' ? (
                      <RunActivityRow
                        activity={item}
                        runSessionTypes={runSessionTypes}
                        openPickerId={openPickerId}
                        onTogglePicker={togglePicker}
                        onSelect={selectRunSessionType}
                      />
                    ) : (
                      <span>{activityLabel(item)}</span>
                    )}
                    {item.notes && <p className="notes">{item.notes}</p>}
                  </div>
                ))}
              </div>
            )
          })}
        </div>
      )}
    </main>
  )
}
