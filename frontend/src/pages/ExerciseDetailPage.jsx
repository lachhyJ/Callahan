import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getExerciseCues, getExerciseHistory, getExerciseStats, updateCue, updateExerciseAssisted, updateExerciseName, updateExerciseTimeBased } from '../api/client'
import CueInput from '../components/CueInput'
import ProgressionChart from '../components/ProgressionChart'
import { formatDateMedium } from '../dateUtils'
import { SET_TYPE_LABELS, formatWeight } from '../utils/format'

const PAGE_SIZE = 10

const CHART_RANGES = [
  { label: '3M', days: 90 },
  { label: '6M', days: 180 },
  { label: '1Y', days: 365 },
  { label: 'All', days: null },
]

function filterChartByRange(chart, days) {
  if (days === null) return chart
  const cutoff = Date.now() - days * 24 * 60 * 60 * 1000
  return chart.filter((p) => new Date(`${p.date}T00:00:00`).getTime() >= cutoff)
}

export default function ExerciseDetailPage() {
  const { exerciseId } = useParams()
  const [stats, setStats] = useState(null)
  const [statsError, setStatsError] = useState(null)
  const [history, setHistory] = useState([])
  const [totalSessions, setTotalSessions] = useState(0)
  const [historyError, setHistoryError] = useState(null)
  const [loadingMore, setLoadingMore] = useState(false)
  const [cues, setCues] = useState([])
  const [editingName, setEditingName] = useState(false)
  const [nameDraft, setNameDraft] = useState('')
  const [chartRangeDays, setChartRangeDays] = useState(CHART_RANGES[CHART_RANGES.length - 1].days)

  useEffect(() => {
    setStats(null)
    setStatsError(null)
    setHistory([])
    setHistoryError(null)
    setCues([])
    setEditingName(false)
    getExerciseStats(exerciseId).then(setStats).catch((err) => setStatsError(err.message))
    getExerciseHistory(exerciseId, PAGE_SIZE, 0)
      .then((page) => {
        setHistory(page.entries)
        setTotalSessions(page.totalSessions)
      })
      .catch((err) => setHistoryError(err.message))
    getExerciseCues(exerciseId).then(setCues).catch(() => {})
  }, [exerciseId])

  function updateCueLocal(workoutTemplateExerciseId, value) {
    setCues((prev) => prev.map((c) => (c.workoutTemplateExerciseId !== workoutTemplateExerciseId ? c : { ...c, cue: value })))
  }

  function handleCueBlur(workoutTemplateExerciseId, value) {
    updateCue(workoutTemplateExerciseId, value.trim() || null).catch(() => {})
  }

  function handleAssistedToggle() {
    const isAssisted = !stats.isAssisted
    setStats((prev) => ({ ...prev, isAssisted }))
    updateExerciseAssisted(exerciseId, isAssisted).catch(() => {
      setStats((prev) => ({ ...prev, isAssisted: !isAssisted }))
    })
  }

  // Time-based and per-side move together — the server takes both on one call,
  // and per-side is only meaningful while time-based is on. Turning time-based
  // off leaves per-side untouched so flipping it back doesn't lose the setting.
  function handleTimeBasedToggle() {
    const isTimeBased = !stats.isTimeBased
    const prev = { isTimeBased: stats.isTimeBased, isPerSide: stats.isPerSide }
    setStats((s) => ({ ...s, isTimeBased }))
    updateExerciseTimeBased(exerciseId, isTimeBased, stats.isPerSide, stats.perSideDelaySeconds).catch(() => {
      setStats((s) => ({ ...s, ...prev }))
    })
  }

  function handlePerSideToggle() {
    const isPerSide = !stats.isPerSide
    const prev = { isTimeBased: stats.isTimeBased, isPerSide: stats.isPerSide }
    setStats((s) => ({ ...s, isPerSide }))
    updateExerciseTimeBased(exerciseId, stats.isTimeBased, isPerSide, stats.perSideDelaySeconds).catch(() => {
      setStats((s) => ({ ...s, ...prev }))
    })
  }

  // The gap the workout timer waits between side one ending and side two
  // auto-starting. Typed freely, clamped to 0..60 and persisted on blur; the
  // server clamps too.
  function handlePerSideDelayChange(value) {
    setStats((s) => ({ ...s, perSideDelaySeconds: value }))
  }

  function handlePerSideDelayBlur() {
    const parsed = Math.round(Number(stats.perSideDelaySeconds))
    const seconds = Number.isFinite(parsed) ? Math.min(60, Math.max(0, parsed)) : 8
    setStats((s) => ({ ...s, perSideDelaySeconds: seconds }))
    updateExerciseTimeBased(exerciseId, stats.isTimeBased, stats.isPerSide, seconds).catch(() => {})
  }

  function startEditingName() {
    setNameDraft(stats.exerciseName)
    setEditingName(true)
  }

  function saveName() {
    const trimmed = nameDraft.trim()
    setEditingName(false)
    if (trimmed === '' || trimmed === stats.exerciseName) return
    const previous = stats.exerciseName
    setStats((prev) => ({ ...prev, exerciseName: trimmed }))
    updateExerciseName(exerciseId, trimmed).catch(() => {
      setStats((prev) => ({ ...prev, exerciseName: previous }))
    })
  }

  function loadMore() {
    setLoadingMore(true)
    getExerciseHistory(exerciseId, PAGE_SIZE, history.length)
      .then((page) => setHistory((prev) => [...prev, ...page.entries]))
      .catch((err) => setHistoryError(err.message))
      .finally(() => setLoadingMore(false))
  }

  if (statsError) {
    return (
      <main className="page">
        <p className="error">{statsError}</p>
      </main>
    )
  }

  if (!stats) {
    return (
      <main className="page">
        <p>Loading exercise…</p>
      </main>
    )
  }

  const hasData = stats.chart.length > 0
  const filteredChart = filterChartByRange(stats.chart, chartRangeDays)

  return (
    <main className="page exercise-detail-page">
      {editingName ? (
        <input
          type="text"
          className="exercise-name-edit"
          value={nameDraft}
          onChange={(e) => setNameDraft(e.target.value)}
          onBlur={saveName}
          onKeyDown={(e) => {
            if (e.key === 'Enter') e.target.blur()
            if (e.key === 'Escape') setEditingName(false)
          }}
          autoFocus
          aria-label="Exercise name"
        />
      ) : (
        <h1 onClick={startEditingName} className="exercise-name-h1" title="Tap to rename">{stats.exerciseName}</h1>
      )}
      <p className="exercise-meta-row">
        {stats.primaryMuscle && <span className="primary-muscle">{stats.primaryMuscle}</span>}
        <button
          type="button"
          className={stats.isAssisted ? 'assisted-toggle active' : 'assisted-toggle'}
          onClick={handleAssistedToggle}
          title="Assisted exercises get a sign-toggle on the weight field during workouts"
        >
          Assisted
        </button>
        <button
          type="button"
          className={stats.isTimeBased ? 'assisted-toggle active' : 'assisted-toggle'}
          onClick={handleTimeBasedToggle}
          title="Time-based exercises log a hold in seconds instead of reps, with an inline countdown"
        >
          Timed
        </button>
        {stats.isTimeBased && (
          <button
            type="button"
            className={stats.isPerSide ? 'assisted-toggle active' : 'assisted-toggle'}
            onClick={handlePerSideToggle}
            title="The workout timer re-prompts for a second side"
          >
            Per side
          </button>
        )}
        {stats.isTimeBased && stats.isPerSide && (
          <label className="per-side-delay">
            Side 2 after
            <input
              type="number"
              inputMode="numeric"
              min={0}
              max={60}
              value={stats.perSideDelaySeconds ?? 8}
              onChange={(e) => handlePerSideDelayChange(e.target.value)}
              onBlur={handlePerSideDelayBlur}
              aria-label="Seconds before side two starts"
            />
            s
          </label>
        )}
      </p>

      {cues.map((c) => (
        <CueInput
          key={c.workoutTemplateExerciseId}
          placeholder={cues.length > 1 ? `What to focus on for ${c.templateName}…` : 'What to focus on for this exercise…'}
          value={c.cue ?? ''}
          onChange={(e) => updateCueLocal(c.workoutTemplateExerciseId, e.target.value)}
          onBlur={() => handleCueBlur(c.workoutTemplateExerciseId, c.cue ?? '')}
          ariaLabel={cues.length > 1 ? `Focus cue for ${c.templateName}` : 'Focus cue for this exercise'}
        />
      ))}

      {!hasData && (
        <div className="empty-state">
          <p>No sets logged for this exercise yet.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}

      {hasData && stats.chart.length >= 2 && (
        <div className="chart-range-toggle">
          {CHART_RANGES.map((r) => (
            <button
              key={r.label}
              type="button"
              className={r.days === chartRangeDays ? 'chart-range-btn active' : 'chart-range-btn'}
              onClick={() => setChartRangeDays(r.days)}
            >
              {r.label}
            </button>
          ))}
        </div>
      )}
      {hasData && stats.chart.length >= 2 && filteredChart.length >= 2 && <ProgressionChart points={filteredChart} />}
      {hasData && stats.chart.length >= 2 && filteredChart.length < 2 && (
        <p className="chart-single-point-note">No sessions logged in this range.</p>
      )}
      {hasData && stats.chart.length === 1 && (
        <p className="chart-single-point-note">
          Logged once so far — {stats.chart[0].maxWeightKg} kg on {formatDateMedium(stats.chart[0].date)}. One more session and you'll see a trend here.
        </p>
      )}

      {hasData && (
        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-label">Heaviest weight</span>
            <span className="stat-value">{formatWeight(stats.heaviestWeightKg)} kg</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">Best est. 1RM</span>
            <span className="stat-value">{formatWeight(stats.bestEstimated1Rm)} kg</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">Best set volume</span>
            <span className="stat-value">{formatWeight(stats.bestSetVolume)} kg</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">Best session volume</span>
            <span className="stat-value">{formatWeight(stats.bestSessionVolume)} kg</span>
          </div>
        </div>
      )}

      {hasData && (
        <div className="exercise-history-section">
          <h2>Session history</h2>
          {historyError && <p className="error">{historyError}</p>}
          {history.length === 0 && !historyError && <p>Loading…</p>}
          {history.map((entry) => (
            <div key={entry.workoutSessionId} className="history-entry">
              <Link to={`/sessions/${entry.workoutSessionId}`} className="history-entry-link"><strong>{formatDateMedium(entry.date)}</strong></Link>
              {entry.notes && <p className="notes">{entry.notes}</p>}
              <ul className="history-set-list">
                {entry.sets.map((s) => (
                  <li key={s.setOrder}>
                    <span className="history-set-number">Set {s.setOrder + 1}</span>
                    <span className={`history-set-type set-type-${s.setType.toLowerCase()}`}>{SET_TYPE_LABELS[s.setType]}</span>
                    <span>{s.reps} × {formatWeight(s.weightKg)} kg</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
          {history.length < totalSessions && (
            <button type="button" className="secondary-btn" onClick={loadMore} disabled={loadingMore}>
              {loadingMore ? 'Loading…' : `Load more (${totalSessions - history.length} left)`}
            </button>
          )}
        </div>
      )}
    </main>
  )
}
