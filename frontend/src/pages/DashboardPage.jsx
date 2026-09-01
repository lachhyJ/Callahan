import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getActivities, getLatestWellness, getMonthlyReports, getWellness, getWellnessInsight, getWorkoutSessions, markMonthlyReportViewed } from '../api/client'
import { buildDailySeries, wellnessRange } from '../wellnessMetrics'
import { isoDate, startOfWeek } from '../dateUtils'
import WellnessCard from '../components/WellnessCard'
import DayDetailSheet from '../components/DayDetailSheet'
import SyncGarminButton from '../components/SyncGarminButton'
import { ChartIcon, CheckIcon, ChevronRightIcon, DocumentIcon, FlameIcon, HistoryIcon, ListIcon, ReportIcon, TaperIcon, TrashIcon } from '../icons'

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

const WEEKDAY_LABELS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']
const MONTH_FORMAT = { month: 'long', year: 'numeric' }

// Shape encodes the session subtype within a family dot's colour. A shared
// 4-shape vocabulary (disc / triangle / diamond / ring) is reused across
// Running and Ultimate — colour separates the families, so the shapes don't
// need to. Ultimate's 5 classification types are still grouped down to keep
// the read easy (Solo / Throws+Pod / Club Training+Game — disc reads as a
// disc, fitting Solo); unclassified activities of either family fall back to
// that family's misc shape.
const RUNNING_SHAPE_CLASS_BY_TYPE_NAME = {
  'High Speed Intervals': 'calendar-dot--triangle',
  'Speed & Acceleration': 'calendar-dot--diamond',
  'Easy Aerobic Run': 'calendar-dot--disc',
}
const RUNNING_MISC_SHAPE = 'calendar-dot--ring' // unclassified or any other run

const ULTIMATE_SHAPE_CLASS_BY_TYPE_NAME = {
  Solo: 'calendar-dot--disc',
  Throws: 'calendar-dot--ring',
  Pod: 'calendar-dot--ring',
  'Club Training': 'calendar-dot--triangle',
  Game: 'calendar-dot--triangle',
}
const ULTIMATE_MISC_SHAPE = 'calendar-dot--disc'

// First classified activity of the day wins the shape.
function dotShapeClass(activities, map, fallback) {
  const classified = activities.find((a) => a.activitySessionTypeName)
  return classified ? map[classified.activitySessionTypeName] ?? fallback : fallback
}

// Three-column grid the layout was built for; rows fill left to right.
const QUICK_LINKS = [
  { to: '/streaks', label: 'Streaks', Icon: FlameIcon },
  { to: '/trends', label: 'Trends', Icon: ChartIcon },
  { to: '/program', label: 'Program', Icon: DocumentIcon },
  { to: '/taper', label: 'Tapering', Icon: TaperIcon },
  { to: '/reports', label: 'Reports', Icon: ReportIcon },
  { to: '/games', label: 'Games', Icon: HistoryIcon },
  { to: '/exercises', label: 'Exercises', Icon: ListIcon },
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

// Rolling window: `weekCount` Monday-first weeks ending with the week that
// contains `anchorDate`. Every cell is a real date (no leading/trailing blanks),
// so the grid height is fixed regardless of where month boundaries fall.
function buildRollingWeeks(anchorDate, weekCount = 6) {
  const endWeekStart = startOfWeek(anchorDate)
  const weeks = []
  for (let w = 0; w < weekCount; w++) {
    const offset = (w - (weekCount - 1)) * 7 // -35, -28, ... , 0
    const week = []
    for (let d = 0; d < 7; d++) {
      week.push(new Date(endWeekStart.getFullYear(), endWeekStart.getMonth(), endWeekStart.getDate() + offset + d))
    }
    weeks.push(week)
  }
  return weeks
}

// The calendar view (rolling vs. month) is remembered for 90 minutes from the
// last toggle - long enough to hold "month" for the duration of a workout, but
// a cold open later in the day drops back to the rolling default. The stored
// timestamp is never refreshed on read, so opening the app doesn't extend it.
const CALENDAR_VIEW_KEY = 'callahan.calendarView'
const CALENDAR_VIEW_TTL_MS = 90 * 60 * 1000

function readStoredCalendarView() {
  try {
    const stored = JSON.parse(localStorage.getItem(CALENDAR_VIEW_KEY))
    if (stored?.mode === 'month' && Date.now() - stored.ts < CALENDAR_VIEW_TTL_MS) return 'month'
  } catch {
    // no/blocked/malformed storage - fall through to the default
  }
  return 'rolling'
}

function writeStoredCalendarView(mode) {
  try {
    localStorage.setItem(CALENDAR_VIEW_KEY, JSON.stringify({ mode, ts: Date.now() }))
  } catch {
    // storage unavailable - the view just won't persist, which is fine
  }
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
  const [calendarView, setCalendarView] = useState(readStoredCalendarView)
  const [selectedDate, setSelectedDate] = useState(null)
  const [wellness, setWellness] = useState(null)
  const [wellnessInsight, setWellnessInsight] = useState(null)
  const [readinessSeries, setReadinessSeries] = useState(null)
  const [savedMessage, setSavedMessage] = useState(location.state?.savedMessage ?? null)
  const [syncResult, setSyncResult] = useState(null) // { text, isError } from the header Sync button
  const [unviewedReport, setUnviewedReport] = useState(null)

  useEffect(() => {
    // Own effect, own silent catch - a wellness fetch failure must never
    // blank the whole dashboard the way the workouts/activities Promise.all
    // below does via the page-level `error` state.
    getLatestWellness().then(setWellness).catch(() => {})
    getWellnessInsight().then(setWellnessInsight).catch(() => {})
    const { start, end } = wellnessRange(30)
    getWellness(start, end)
      .then((rows) => setReadinessSeries(buildDailySeries(rows, 30).byKey.readiness))
      .catch(() => {})
  }, [])

  useEffect(() => {
    getMonthlyReports().then((reports) => {
      // Newest first from the API. Only nudge about a month that has
      // actually ended — the API also returns the current, in-progress
      // month as a provisional entry, and surfacing that produces a
      // misleading "Down month — 0 sessions" headline early in the month.
      const now = new Date()
      const currentMonthKey = now.getFullYear() * 12 + now.getMonth()
      const latestUnviewed = reports.find(
        (r) => !r.viewed && r.year * 12 + (r.month - 1) < currentMonthKey,
      )
      setUnviewedReport(latestUnviewed ?? null)
    }).catch(() => {})
  }, [])

  function dismissUnviewedReport() {
    if (!unviewedReport) return
    markMonthlyReportViewed(unviewedReport.year, unviewedReport.month).catch(() => {})
    setUnviewedReport(null)
  }

  useEffect(() => {
    if (!savedMessage) return
    const timeout = setTimeout(() => setSavedMessage(null), 4000)
    return () => clearTimeout(timeout)
  }, [savedMessage])

  useEffect(() => {
    if (!syncResult) return
    const timeout = setTimeout(() => setSyncResult(null), 6000)
    return () => clearTimeout(timeout)
  }, [syncResult])

  function loadSessions() {
    Promise.all([getWorkoutSessions(), getActivities()])
      .then(([w, a]) => {
        setWorkouts(w)
        setActivities(a)
      })
      .catch((err) => setError(err.message))
  }

  useEffect(loadSessions, [])

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
  if (workouts === null || activities === null) return <main className="page"><p>Loading dashboard…</p></main>

  const hasAnyHistory = workouts.length > 0 || activities.length > 0
  const now = new Date()
  const isMonthView = calendarView === 'month'
  const weeks = isMonthView ? buildMonthGrid(cursor.year, cursor.month) : buildRollingWeeks(now)
  const todayIso = isoDate(new Date())
  const currentWeekStartIso = isoDate(startOfWeek(new Date()))
  const monthLabel = new Date(cursor.year, cursor.month, 1).toLocaleDateString(undefined, MONTH_FORMAT)
  const isViewingCurrentMonth = cursor.year === now.getFullYear() && cursor.month === now.getMonth()
  const selectedEntry = selectedDate ? byDate.get(selectedDate) : null

  function changeMonth(delta) {
    setSelectedDate(null)
    setCursor((prev) => {
      const d = new Date(prev.year, prev.month + delta, 1)
      return { year: d.getFullYear(), month: d.getMonth() }
    })
  }

  function goToCurrentMonth() {
    setSelectedDate(null)
    setCursor({ year: now.getFullYear(), month: now.getMonth() })
  }

  function toggleCalendarView() {
    const next = isMonthView ? 'rolling' : 'month'
    setSelectedDate(null)
    if (next === 'month') setCursor({ year: now.getFullYear(), month: now.getMonth() })
    setCalendarView(next)
    writeStoredCalendarView(next)
  }

  return (
    <main className="page dashboard-page">
      <h1 className="sr-only">Dashboard</h1>
      <div className="dashboard-header">
        <div className="dashboard-header-actions">
          <button type="button" className="calendar-view-toggle" onClick={toggleCalendarView}>
            {isMonthView ? 'Recent' : 'Month view'}
          </button>
          <SyncGarminButton variant="icon" onSynced={loadSessions} onResult={setSyncResult} />
          <Link to="/recently-deleted" className="icon-link" aria-label="Recently deleted">
            <TrashIcon />
          </Link>
        </div>
      </div>

      {savedMessage && (
        <p className="save-confirmation"><CheckIcon /> {savedMessage}</p>
      )}

      {syncResult && (
        <p className={syncResult.isError ? 'save-confirmation error' : 'save-confirmation'}>
          {!syncResult.isError && <CheckIcon />} {syncResult.text}
        </p>
      )}

      {isMonthView && (
        <div className="calendar-nav">
          <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeMonth(-1)} aria-label="Previous month">
            ‹
          </button>
          <span className="calendar-month-label">{monthLabel}</span>
          <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeMonth(1)} aria-label="Next month">
            ›
          </button>
          {!isViewingCurrentMonth && (
            <button type="button" className="calendar-view-toggle calendar-today-btn" onClick={goToCurrentMonth}>
              Today
            </button>
          )}
        </div>
      )}

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

          // Rolling view spans month boundaries, so mark the row that contains a
          // 1st (never the top row - nothing above it to divide from) and tag
          // its gutter with the new month's name.
          const firstOfMonthCell = !isMonthView && wi > 0 ? week.find((d) => d && d.getDate() === 1) : null
          const monthTag = firstOfMonthCell
            ? firstOfMonthCell.toLocaleDateString(undefined, { month: 'short' })
            : null

          const dayCells = week.map((date, di) => {
            if (!date) return <div key={`${wi}-${di}`} className="calendar-cell calendar-cell-blank" />
            const iso = isoDate(date)
            const entry = byDate.get(iso)
            const hasWorkout = entry?.workouts.length > 0
            const runningActivities = entry?.runs.filter((r) => r.type === 'Running') ?? []
            const hasRunning = runningActivities.length > 0
            const ultimateActivities = entry?.runs.filter((r) => r.type === 'Ultimate') ?? []
            const hasUltimate = ultimateActivities.length > 0
            const hasData = hasWorkout || hasRunning || hasUltimate
            const isToday = iso === todayIso
            const isSelected = iso === selectedDate

            const dayNumber = (
              <span className={isToday ? 'calendar-day-number today' : 'calendar-day-number'}>{date.getDate()}</span>
            )

            if (!hasData) {
              return (
                <div key={iso} className={`calendar-cell${isToday ? ' calendar-cell-today' : ''}`}>
                  {dayNumber}
                </div>
              )
            }

            return (
              <button
                key={iso}
                type="button"
                className={`calendar-cell calendar-cell-active${isToday ? ' calendar-cell-today' : ''}${isSelected ? ' selected' : ''}`}
                onClick={() => setSelectedDate(isSelected ? null : iso)}
              >
                {dayNumber}
                <span className="calendar-dots">
                  {hasWorkout && <span className="calendar-dot calendar-dot-workout" />}
                  {hasRunning && <span className={`calendar-dot calendar-dot-run ${dotShapeClass(runningActivities, RUNNING_SHAPE_CLASS_BY_TYPE_NAME, RUNNING_MISC_SHAPE)}`} />}
                  {hasUltimate && <span className={`calendar-dot calendar-dot-ultimate ${dotShapeClass(ultimateActivities, ULTIMATE_SHAPE_CLASS_BY_TYPE_NAME, ULTIMATE_MISC_SHAPE)}`} />}
                </span>
              </button>
            )
          })

          const isFutureWeek = weekStartIso && weekStartIso > currentWeekStartIso
          const gutter = weekStartIso && !isFutureWeek ? (
            <button
              key={`gutter-${wi}`}
              type="button"
              className="calendar-week-gutter"
              aria-label={`View sessions for the week of ${weekStartIso}`}
              onClick={() => navigate(`/history?week=${weekStartIso}`)}
            >
              {monthTag && <span className="calendar-gutter-month">{monthTag}</span>}
              <ChevronRightIcon />
            </button>
          ) : (
            <div key={`gutter-${wi}`} className="calendar-week-gutter calendar-cell-blank" />
          )

          const divider = monthTag
            ? <div key={`divider-${wi}`} className="calendar-month-divider" />
            : null

          return divider ? [divider, gutter, ...dayCells] : [gutter, ...dayCells]
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

      {unviewedReport && (
        <div className="save-confirmation section-gap" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Link to={`/reports/${unviewedReport.year}/${unviewedReport.month}`}>
            {MONTH_NAMES[unviewedReport.month - 1]} {unviewedReport.year} report is ready — {unviewedReport.headlineVerdict}
          </Link>
          <button type="button" className="secondary-btn" onClick={dismissUnviewedReport}>Dismiss</button>
        </div>
      )}

      {wellness && (
        <div className="section-gap">
          <WellnessCard wellness={wellness} todayIso={todayIso} insight={wellnessInsight} readinessSeries={readinessSeries} />
        </div>
      )}

      <DayDetailSheet date={selectedDate} entry={selectedEntry} onClose={() => setSelectedDate(null)} />
    </main>
  )
}
