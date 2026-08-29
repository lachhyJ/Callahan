import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getActivities, getLatestWellness, getMonthlyReports, getWeeklyVolume, getWellness, getWellnessInsight, getWorkoutSessions, markMonthlyReportViewed } from '../api/client'
import { buildDailySeries, wellnessRange } from '../wellnessMetrics'
import { isoDate, startOfWeek } from '../dateUtils'
import WeeklyVolumeChart from '../components/WeeklyVolumeChart'
import WellnessCard from '../components/WellnessCard'
import DayDetailSheet from '../components/DayDetailSheet'
import SyncGarminButton from '../components/SyncGarminButton'
import { ChartIcon, CheckIcon, ChevronRightIcon, DocumentIcon, FlameIcon, HistoryIcon, ListIcon, TaperIcon, TrashIcon } from '../icons'

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

const WEEKDAY_LABELS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']
const MONTH_FORMAT = { month: 'long', year: 'numeric' }

// Shape is the subtype signal within the Ultimate dot's colour, grouped to
// keep the count low enough to read at the dot's 6px size (Solo+Throws /
// Pod / Club Training+Game — 3 shapes, not 5 for the 5 classification
// types). Unclassified Ultimate activities fall back to the plain circle,
// same shape as the largest group, rather than a fourth visual state.
const ULTIMATE_SHAPE_CLASS_BY_TYPE_NAME = {
  Solo: 'calendar-dot-ultimate-circle',
  Throws: 'calendar-dot-ultimate-circle',
  Pod: 'calendar-dot-ultimate-square',
  'Club Training': 'calendar-dot-ultimate-triangle',
  Game: 'calendar-dot-ultimate-triangle',
}

function ultimateDotShapeClass(ultimateActivities) {
  const classified = ultimateActivities.find((a) => a.activitySessionTypeName)
  return classified
    ? ULTIMATE_SHAPE_CLASS_BY_TYPE_NAME[classified.activitySessionTypeName] ?? 'calendar-dot-ultimate-circle'
    : 'calendar-dot-ultimate-circle'
}

// Three-column grid the layout was built for; rows fill left to right.
const QUICK_LINKS = [
  { to: '/streaks', label: 'Streaks', Icon: FlameIcon },
  { to: '/trends', label: 'Trends', Icon: ChartIcon },
  { to: '/program', label: 'Program', Icon: DocumentIcon },
  { to: '/taper', label: 'Tapering', Icon: TaperIcon },
  { to: '/reports', label: 'Reports', Icon: DocumentIcon },
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
  const [wellness, setWellness] = useState(null)
  const [wellnessInsight, setWellnessInsight] = useState(null)
  const [readinessSeries, setReadinessSeries] = useState(null)
  const [savedMessage, setSavedMessage] = useState(location.state?.savedMessage ?? null)
  const [syncResult, setSyncResult] = useState(null) // { text, isError } from the header Sync button
  const [unviewedReport, setUnviewedReport] = useState(null)

  useEffect(() => {
    getWeeklyVolume(8).then(setWeeklyVolume).catch(() => {})
  }, [])

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
      // Newest first from the API — the latest finalized-or-provisional
      // report that hasn't been opened yet, if any.
      const latestUnviewed = reports.find((r) => !r.viewed)
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
  const hasUnviewedReport = unviewedReport != null
  const weeks = buildMonthGrid(cursor.year, cursor.month)
  const todayIso = isoDate(new Date())
  const currentWeekStartIso = isoDate(startOfWeek(new Date()))
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
      <div className="dashboard-header">
        <h1>Dashboard</h1>
        <div className="dashboard-header-actions">
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
                  {hasUltimate && <span className={`calendar-dot calendar-dot-ultimate ${ultimateDotShapeClass(ultimateActivities)}`} />}
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
              <span>{label}{to === '/reports' && hasUnviewedReport && <span className="report-unviewed-dot" aria-label="New report" />}</span>
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

      {weeklyVolume && <WeeklyVolumeChart weeks={weeklyVolume} />}

      <DayDetailSheet date={selectedDate} entry={selectedEntry} onClose={() => setSelectedDate(null)} />
    </main>
  )
}
