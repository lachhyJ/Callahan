import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMuscleBalance } from '../api/client'
import { endOfWeek, isoDate, startOfWeek } from '../dateUtils'
import MuscleHeatmap from '../components/MuscleHeatmap'

const WEEK_FORMAT = { month: 'short', day: 'numeric' }

function formatSetCount(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

// Keeps a sliver visible for near-zero counts, mirroring the old min-width: 4px.
function barScale(setCount, maxCount) {
  return Math.max(setCount / maxCount, 0.02)
}

export default function MuscleBalancePage() {
  const [cursor, setCursor] = useState(() => new Date())
  const [balance, setBalance] = useState(null)
  const [error, setError] = useState(null)

  const weekStart = startOfWeek(cursor)
  const weekEnd = endOfWeek(cursor)

  useEffect(() => {
    setBalance(null)
    setError(null)
    getMuscleBalance(isoDate(weekStart), isoDate(weekEnd))
      .then(setBalance)
      .catch((err) => setError(err.message))
  }, [isoDate(weekStart)]) // eslint-disable-line react-hooks/exhaustive-deps

  function changeWeek(deltaWeeks) {
    setCursor((prev) => {
      const next = new Date(prev)
      next.setDate(prev.getDate() + deltaWeeks * 7)
      return next
    })
  }

  const weekLabel = `${weekStart.toLocaleDateString(undefined, WEEK_FORMAT)} – ${weekEnd.toLocaleDateString(undefined, WEEK_FORMAT)}`
  const maxCount = balance ? Math.max(...balance.map((b) => b.setCount), 1) : 1
  const hasAnySets = balance?.some((b) => b.setCount > 0)

  return (
    <main className="page">
      <h1>Muscle balance</h1>

      <div className="calendar-nav section-gap">
        <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeWeek(-1)} aria-label="Previous week">
          ‹
        </button>
        <span className="calendar-month-label">{weekLabel}</span>
        <button type="button" className="secondary-btn calendar-nav-btn" onClick={() => changeWeek(1)} aria-label="Next week">
          ›
        </button>
      </div>

      {error && <p className="error">{error}</p>}
      {!error && balance === null && <p>Loading…</p>}

      {balance && !hasAnySets && (
        <div className="empty-state section-gap">
          <p>No sets logged this week.</p>
          <Link to="/" className="custom-workout-link">Start a workout</Link>
        </div>
      )}

      {balance && hasAnySets && (
        <div className="section-gap">
          <MuscleHeatmap balance={balance} />
        </div>
      )}

      {balance && hasAnySets && (
        <div className="muscle-bar-list section-gap">
          {balance.map((b) => (
            <div key={b.muscleGroup} className="muscle-bar-row">
              <span className="muscle-bar-label" title={b.muscleGroup}>{b.muscleGroup}</span>
              <div className="muscle-bar-track">
                <div className="muscle-bar-fill" style={{ transform: `scaleX(${barScale(b.setCount, maxCount)})` }} />
              </div>
              <span className="muscle-bar-value">{formatSetCount(b.setCount)}</span>
            </div>
          ))}
        </div>
      )}

      {balance && hasAnySets && (
        <p className="page-subtitle section-gap">
          Sets per muscle group this week — a set counts full toward its primary muscle, half toward each secondary one.
        </p>
      )}
    </main>
  )
}
