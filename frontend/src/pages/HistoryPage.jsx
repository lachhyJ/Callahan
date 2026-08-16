import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getActivities, getRunSessionTypes, getWorkoutSessions, updateActivityRunSessionType } from '../api/client'
import { activityLabel } from '../utils/activityLabel'
import { workoutLabel } from '../components/SessionList'
import RunActivityRow from '../components/RunActivityRow'
import { isoDate, shortWeekdayAndDay, startOfWeek } from '../dateUtils'

const WEEKS_PER_PAGE = 6

function formatWeekLabel(weekStartIso) {
  const start = new Date(`${weekStartIso}T00:00:00`)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`
}

function addWeeks(d, n) {
  const result = new Date(d)
  result.setDate(result.getDate() + n * 7)
  return result
}

// startOfWeek(new Date()) carries whatever time-of-day "now" happened to be
// at the call site, not midnight — comparing two such values captured at
// different moments (e.g. a click handler vs. a later render) can come out
// "before" even on the same calendar day. Compare via the date string
// instead, which is immune to that drift.
function isAtOrAfter(d, reference) {
  return isoDate(d) >= isoDate(reference)
}

function capToWeek(d, cap) {
  return isoDate(d) > isoDate(cap) ? cap : d
}

// Groups items into every Monday-first week between rangeStart and
// rangeEnd inclusive — including weeks with nothing logged, so a dry
// spell reads as a run of empty weeks rather than a gap in the list.
// Only ever grouped over whatever's currently loaded, not "everything" —
// see loadEarlier/loadLater below for how that window grows.
//
// Weeks themselves stay newest-first (a normal history feed), but each
// week's own items go Monday → Sunday — the items list inherits the
// caller's overall newest-first sort, which would otherwise show Sunday's
// session above Monday's inside the same box, backwards from how the week
// header itself and the Dashboard's Monday-first grid read.
function groupByWeek(items, rangeStart, rangeEnd) {
  const byWeekStart = new Map()
  for (const item of items) {
    const weekStart = isoDate(startOfWeek(new Date(`${item.date}T00:00:00`)))
    if (!byWeekStart.has(weekStart)) byWeekStart.set(weekStart, [])
    byWeekStart.get(weekStart).push(item)
  }
  for (const weekItems of byWeekStart.values()) {
    weekItems.sort((a, b) => a.date.localeCompare(b.date))
  }

  const weeks = []
  for (const d = new Date(rangeEnd); d >= rangeStart; d.setDate(d.getDate() - 7)) {
    const weekStart = isoDate(d)
    weeks.push({ weekStart, items: byWeekStart.get(weekStart) ?? [] })
  }
  return weeks
}

export default function HistoryPage() {
  const [searchParams] = useSearchParams()
  const targetWeek = searchParams.get('week')

  // The loaded window is [rangeStart, rangeEnd] (both Monday-first week
  // starts) — grows outward from the target week (or today, if opened
  // without one) as the user asks for earlier/later weeks, rather than
  // ever fetching the whole history at once.
  const initialCenter = useMemo(
    () => startOfWeek(targetWeek ? new Date(`${targetWeek}T00:00:00`) : new Date()),
    [] // eslint-disable-line react-hooks/exhaustive-deps
  )
  const [range, setRange] = useState(() => ({
    start: addWeeks(initialCenter, -WEEKS_PER_PAGE),
    end: capToWeek(addWeeks(initialCenter, WEEKS_PER_PAGE), startOfWeek(new Date())),
  }))

  const [items, setItems] = useState(null)
  const [error, setError] = useState(null)
  const [loadingEarlier, setLoadingEarlier] = useState(false)
  const [loadingLater, setLoadingLater] = useState(false)
  const [runSessionTypes, setRunSessionTypes] = useState([])
  const [openPickerId, setOpenPickerId] = useState(null)
  const targetRef = useRef(null)
  const hasScrolledToTarget = useRef(false)

  useEffect(() => {
    const start = isoDate(range.start)
    const end = isoDate(range.end)
    Promise.all([getWorkoutSessions({ start, end }), getActivities({ start, end }), getRunSessionTypes()])
      .then(([workouts, activities, types]) => {
        const merged = [
          ...workouts.map((w) => ({ kind: 'workout', ...w })),
          ...activities.map((a) => ({ kind: 'activity', ...a })),
        ].sort((a, b) => b.date.localeCompare(a.date))
        setItems(merged)
        setRunSessionTypes(types)
      })
      .catch((err) => setError(err.message))
    // Only the initial range — loadEarlier/loadLater fetch and merge
    // their own slice directly instead of re-running this whole effect.
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const weeks = items ? groupByWeek(items, range.start, range.end) : []
  const caughtUpToToday = isAtOrAfter(range.end, startOfWeek(new Date()))

  useEffect(() => {
    if (targetWeek && targetRef.current && !hasScrolledToTarget.current) {
      targetRef.current.scrollIntoView({ block: 'center' })
      hasScrolledToTarget.current = true
    }
  }, [targetWeek, items])

  async function loadEarlier() {
    setLoadingEarlier(true)
    const newStart = addWeeks(range.start, -WEEKS_PER_PAGE)
    const start = isoDate(newStart)
    const end = isoDate(addWeeks(range.start, -1))
    try {
      const [workouts, activities] = await Promise.all([getWorkoutSessions({ start, end }), getActivities({ start, end })])
      const older = [
        ...workouts.map((w) => ({ kind: 'workout', ...w })),
        ...activities.map((a) => ({ kind: 'activity', ...a })),
      ]
      setItems((current) => [...current, ...older].sort((a, b) => b.date.localeCompare(a.date)))
      setRange((current) => ({ ...current, start: newStart }))
    } catch (err) {
      setError(err.message)
    } finally {
      setLoadingEarlier(false)
    }
  }

  async function loadLater() {
    setLoadingLater(true)
    const start = isoDate(addWeeks(range.end, 1))
    const newEnd = capToWeek(addWeeks(range.end, WEEKS_PER_PAGE), startOfWeek(new Date()))
    const end = isoDate(newEnd)
    try {
      const [workouts, activities] = await Promise.all([getWorkoutSessions({ start, end }), getActivities({ start, end })])
      const newer = [
        ...workouts.map((w) => ({ kind: 'workout', ...w })),
        ...activities.map((a) => ({ kind: 'activity', ...a })),
      ]
      setItems((current) => [...newer, ...current].sort((a, b) => b.date.localeCompare(a.date)))
      setRange((current) => ({ ...current, end: newEnd }))
    } catch (err) {
      setError(err.message)
    } finally {
      setLoadingLater(false)
    }
  }

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
      <h1>History</h1>
      {error && <p className="error">{error}</p>}
      {items === null && !error && <p>Loading history…</p>}
      {weeks.length > 0 && (
        <>
          {!caughtUpToToday && (
            <button type="button" className="secondary-btn history-load-btn" onClick={loadLater} disabled={loadingLater}>
              {loadingLater ? 'Loading…' : 'Load later weeks'}
            </button>
          )}

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
                  {week.items.map((item) => {
                    // A classified Garmin run's notes are just the raw event name
                    // Garmin gave it (e.g. "Melbourne - HiSpd Intervals") — once
                    // it's tagged with our own type, showing both is redundant.
                    const isClassifiedGarminRun = item.type === 'Running' && item.source === 'Garmin' && item.runSessionTypeId
                    const showNotes = item.notes && !isClassifiedGarminRun

                    return (
                      <div key={`${item.kind}-${item.id}`} className="history-item">
                        <strong>{shortWeekdayAndDay(item.date)}</strong>{' '}
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
                        {showNotes && <p className="notes">{item.notes}</p>}
                      </div>
                    )
                  })}
                </div>
              )
            })}
          </div>

          <button type="button" className="secondary-btn history-load-btn" onClick={loadEarlier} disabled={loadingEarlier}>
            {loadingEarlier ? 'Loading…' : 'Load earlier weeks'}
          </button>
        </>
      )}
    </main>
  )
}
