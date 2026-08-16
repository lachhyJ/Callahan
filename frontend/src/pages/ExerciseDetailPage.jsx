import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getExerciseCues, getExerciseHistory, getExerciseStats, updateCue } from '../api/client'
import ProgressionChart from '../components/ProgressionChart'

const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }
const PAGE_SIZE = 10

function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
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

  useEffect(() => {
    setStats(null)
    setStatsError(null)
    setHistory([])
    setHistoryError(null)
    setCues([])
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

  return (
    <main className="page exercise-detail-page">
      <h1>{stats.exerciseName}</h1>
      {stats.primaryMuscle && <p className="primary-muscle">{stats.primaryMuscle}</p>}

      {cues.map((c) => (
        <input
          key={c.workoutTemplateExerciseId}
          type="text"
          className="cue-input"
          placeholder={cues.length > 1 ? `What to focus on for ${c.templateName}…` : 'What to focus on for this exercise…'}
          value={c.cue ?? ''}
          onChange={(e) => updateCueLocal(c.workoutTemplateExerciseId, e.target.value)}
          onBlur={() => handleCueBlur(c.workoutTemplateExerciseId, c.cue ?? '')}
          aria-label={cues.length > 1 ? `Focus cue for ${c.templateName}` : 'Focus cue for this exercise'}
        />
      ))}

      {!hasData && (
        <div className="empty-state">
          <p>No sets logged for this exercise yet.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}

      {hasData && stats.chart.length >= 2 && <ProgressionChart points={stats.chart} />}
      {hasData && stats.chart.length === 1 && (
        <p className="chart-single-point-note">
          Logged once so far — {stats.chart[0].maxWeightKg} kg on {stats.chart[0].date}. One more session and you'll see a trend here.
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
              <strong>{entry.date}</strong>
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
