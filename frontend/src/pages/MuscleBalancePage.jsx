import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getMuscleBalance } from '../api/client'
import { BackIcon } from '../icons'
import { endOfWeek, isoDate, startOfWeek } from '../dateUtils'
import MuscleHeatmap from '../components/MuscleHeatmap'

const WEEK_FORMAT = { month: 'short', day: 'numeric' }

function formatSetCount(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

export default function MuscleBalancePage() {
  const navigate = useNavigate()
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
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>Muscle balance</h1>

      <div className="calendar-nav">
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
        <div className="empty-state">
          <p>No sets logged this week.</p>
        </div>
      )}

      {balance && hasAnySets && <MuscleHeatmap balance={balance} />}

      {balance && hasAnySets && (
        <div className="muscle-bar-list">
          {balance.map((b) => (
            <div key={b.muscleGroup} className="muscle-bar-row">
              <span className="muscle-bar-label">{b.muscleGroup}</span>
              <div className="muscle-bar-track">
                <div className="muscle-bar-fill" style={{ width: `${(b.setCount / maxCount) * 100}%` }} />
              </div>
              <span className="muscle-bar-value">{formatSetCount(b.setCount)}</span>
            </div>
          ))}
        </div>
      )}
    </main>
  )
}
