import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { getRunningSessions, getWorkoutSessions } from '../api/client'
import { isoDate } from '../dateUtils'

const WEEKDAY_LABELS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']
const MONTH_FORMAT = { month: 'long', year: 'numeric' }

function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

// Monday-first grid: leading/trailing cells from adjacent months are left blank
// (no number, not interactive) rather than shown faded — matches the reference
// this was modeled on and keeps the grid free of a second visual weight class.
function buildMonthGrid(year, month) {
  const firstOfMonth = new Date(year, month, 1)
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const leadingBlanks = (firstOfMonth.getDay() + 6) % 7 // Mon=0 ... Sun=6

  const cells = []
  for (let i = 0; i < leadingBlanks; i++) cells.push(null)
  for (let day = 1; day <= daysInMonth; day++) cells.push(new Date(year, month, day))
  while (cells.length % 7 !== 0) cells.push(null)

  const weeks = []
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7))
  return weeks
}

export default function CalendarPage() {
  const [workouts, setWorkouts] = useState(null)
  const [runs, setRuns] = useState(null)
  const [error, setError] = useState(null)
  const [cursor, setCursor] = useState(() => {
    const now = new Date()
    return { year: now.getFullYear(), month: now.getMonth() }
  })
  const [selectedDate, setSelectedDate] = useState(null)

  useEffect(() => {
    Promise.all([getWorkoutSessions(), getRunningSessions()])
      .then(([w, r]) => {
        setWorkouts(w)
        setRuns(r)
      })
      .catch((err) => setError(err.message))
  }, [])

  const byDate = useMemo(() => {
    const map = new Map()
    for (const w of workouts ?? []) {
      const entry = map.get(w.date) ?? { workouts: [], runs: [] }
      entry.workouts.push(w)
      map.set(w.date, entry)
    }
    for (const r of runs ?? []) {
      const entry = map.get(r.date) ?? { workouts: [], runs: [] }
      entry.runs.push(r)
      map.set(r.date, entry)
    }
    return map
  }, [workouts, runs])

  if (error) return <main className="page"><p className="error">{error}</p></main>
  if (workouts === null || runs === null) return <main className="page"><p>Loading…</p></main>

  const hasAnyHistory = workouts.length > 0 || runs.length > 0
  const weeks = buildMonthGrid(cursor.year, cursor.month)
  const todayIso = isoDate(new Date())
  const monthLabel = new Date(cursor.year, cursor.month, 1).toLocaleDateString(undefined, MONTH_FORMAT)
  const selectedEntry = selectedDate ? byDate.get(selectedDate) : null

  function changeMonth(delta) {
    setSelectedDate(null)
    setCursor((prev) => {
      const d = new Date(prev.year, prev.month + delta, 1)
      return { year: d.getFullYear(), month: d.getMonth() }
    })
  }

  return (
    <main className="page calendar-page">
      <h1>Calendar</h1>
      <Link to="/muscle-balance" className="session-link">Muscle balance</Link>

      {!hasAnyHistory && (
        <div className="empty-state">
          <p>No sessions logged yet — once you finish a workout or run, it'll show up here.</p>
        </div>
      )}

      <div className="calendar-nav">
        <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeMonth(-1)} aria-label="Previous month">
          ‹
        </button>
        <span className="calendar-month-label">{monthLabel}</span>
        <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeMonth(1)} aria-label="Next month">
          ›
        </button>
      </div>

      <div className="calendar-grid">
        {WEEKDAY_LABELS.map((label, i) => (
          <div key={i} className="calendar-weekday">{label}</div>
        ))}
        {weeks.flatMap((week, wi) =>
          week.map((date, di) => {
            if (!date) return <div key={`${wi}-${di}`} className="calendar-cell calendar-cell-blank" />
            const iso = isoDate(date)
            const entry = byDate.get(iso)
            const hasWorkout = entry?.workouts.length > 0
            const hasRun = entry?.runs.length > 0
            const hasData = hasWorkout || hasRun
            const isToday = iso === todayIso
            const isSelected = iso === selectedDate

            const dayNumber = (
              <span className={isToday ? 'calendar-day-number today' : 'calendar-day-number'}>{date.getDate()}</span>
            )

            if (!hasData) {
              return (
                <div key={iso} className="calendar-cell">
                  {dayNumber}
                </div>
              )
            }

            return (
              <button
                key={iso}
                type="button"
                className={isSelected ? 'calendar-cell calendar-cell-active selected' : 'calendar-cell calendar-cell-active'}
                onClick={() => setSelectedDate(isSelected ? null : iso)}
              >
                {dayNumber}
                <span className="calendar-dots">
                  {hasWorkout && <span className="calendar-dot calendar-dot-workout" />}
                  {hasRun && <span className="calendar-dot calendar-dot-run" />}
                </span>
              </button>
            )
          })
        )}
      </div>

      {selectedEntry && (
        <div className="calendar-detail">
          <strong>
            {new Date(`${selectedDate}T00:00:00`).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
          </strong>
          {selectedEntry.workouts.map((w) => (
            <Link key={`w-${w.id}`} to={`/sessions/${w.id}`} className="session-link">
              {w.templateName ?? 'Workout'} · {w.setCount} set{w.setCount === 1 ? '' : 's'}
            </Link>
          ))}
          {selectedEntry.runs.map((r) => (
            <p key={`r-${r.id}`}>
              Run · {r.distanceKm} km in {formatDuration(r.durationSeconds)}
            </p>
          ))}
        </div>
      )}
    </main>
  )
}
