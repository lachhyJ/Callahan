import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getActivities, getWeeklyVolume, getWorkoutSessions } from '../api/client'
import { isoDate, startOfWeek } from '../dateUtils'
import WeeklyVolumeChart from '../components/WeeklyVolumeChart'
import DayDetailSheet from '../components/DayDetailSheet'
import { ChartIcon, CheckIcon, ChevronRightIcon, DocumentIcon, FlameIcon, ListIcon, TaperIcon } from '../icons'

const WEEKDAY_LABELS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']
const MONTH_FORMAT = { month: 'long', year: 'numeric' }

// Five real destinations, one placeholder — fills the two-row,
// three-column grid the layout was built for.
const QUICK_LINKS = [
  { to: '/muscle-balance', label: 'Muscle balance', Icon: ChartIcon },
  { to: '/exercises', label: 'Exercises', Icon: ListIcon },
  { to: '/streaks', label: 'Streaks', Icon: FlameIcon },
  { to: '/trends', label: 'Trends', Icon: ChartIcon },
  { to: '/program', label: 'Program', Icon: DocumentIcon },
  { label: 'Tapering', Icon: TaperIcon, soon: true },
]

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

export default function DashboardPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const [workouts, setWorkouts] = useState(null)
  const [activities, setActivities] = useState(null)
  const [error, setError] = useState(null)
  const [cursor, setCursor] = useState(() => {
    const now = new Date()
    return { year: now.getFullYear(), month: now.getMonth() }
  })
  const [selectedDate, setSelectedDate] = useState(null)
  const [weeklyVolume, setWeeklyVolume] = useState(null)
  const [savedMessage, setSavedMessage] = useState(location.state?.savedMessage ?? null)

  useEffect(() => {
    getWeeklyVolume(8).then(setWeeklyVolume).catch(() => {})
  }, [])

  useEffect(() => {
    if (!savedMessage) return
    const timeout = setTimeout(() => setSavedMessage(null), 4000)
    return () => clearTimeout(timeout)
  }, [savedMessage])

  useEffect(() => {
    Promise.all([getWorkoutSessions(), getActivities()])
      .then(([w, a]) => {
        setWorkouts(w)
        setActivities(a)
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
    for (const a of activities ?? []) {
      const entry = map.get(a.date) ?? { workouts: [], runs: [] }
      entry.runs.push(a)
      map.set(a.date, entry)
    }
    return map
  }, [workouts, activities])

  if (error) return <main className="page"><p className="error">{error}</p></main>
  if (workouts === null || activities === null) return <main className="page"><p>Loading…</p></main>

  const hasAnyHistory = workouts.length > 0 || activities.length > 0
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
    <main className="page dashboard-page">
      <h1>Dashboard</h1>

      {savedMessage && (
        <p className="save-confirmation"><CheckIcon /> {savedMessage}</p>
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

      {!hasAnyHistory && (
        <div className="empty-state section-gap">
          <p>No sessions logged yet — once you finish a workout or run, it'll show up here.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}

      <div className="calendar-grid">
        <div className="calendar-weekday calendar-gutter-spacer" />
        {WEEKDAY_LABELS.map((label, i) => (
          <div key={i} className="calendar-weekday">{label}</div>
        ))}
        {weeks.flatMap((week, wi) => {
          const firstRealDate = week.find((d) => d)
          const weekStartIso = firstRealDate ? isoDate(startOfWeek(firstRealDate)) : null

          const dayCells = week.map((date, di) => {
            if (!date) return <div key={`${wi}-${di}`} className="calendar-cell calendar-cell-blank" />
            const iso = isoDate(date)
            const entry = byDate.get(iso)
            const hasWorkout = entry?.workouts.length > 0
            const hasRunning = entry?.runs.some((r) => r.type === 'Running')
            const hasUltimate = entry?.runs.some((r) => r.type === 'Ultimate')
            const hasData = hasWorkout || hasRunning || hasUltimate
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
                  {hasRunning && <span className="calendar-dot calendar-dot-run" />}
                  {hasUltimate && <span className="calendar-dot calendar-dot-ultimate" />}
                </span>
              </button>
            )
          })

          const gutter = weekStartIso ? (
            <button
              key={`gutter-${wi}`}
              type="button"
              className="calendar-week-gutter"
              aria-label={`View sessions for the week of ${weekStartIso}`}
              onClick={() => navigate(`/history?week=${weekStartIso}`)}
            >
              <ChevronRightIcon />
            </button>
          ) : (
            <div key={`gutter-${wi}`} className="calendar-week-gutter calendar-cell-blank" />
          )

          return [gutter, ...dayCells]
        })}
      </div>

      <div className="quick-links-grid section-gap">
        {QUICK_LINKS.map(({ to, label, Icon, soon }) =>
          soon ? (
            <div key={label} className="quick-link-tile soon">
              <span className="quick-link-soon-badge">Soon</span>
              <Icon />
              <span>{label}</span>
            </div>
          ) : (
            <Link key={to} to={to} className="quick-link-tile">
              <Icon />
              <span>{label}</span>
            </Link>
          )
        )}
      </div>

      {weeklyVolume && <WeeklyVolumeChart weeks={weeklyVolume} />}

      <DayDetailSheet date={selectedDate} entry={selectedEntry} onClose={() => setSelectedDate(null)} />
    </main>
  )
}
