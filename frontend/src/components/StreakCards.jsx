import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getStreaks } from '../api/client'
import { ChevronRightIcon } from '../icons'

// Streaks block at the top of Trends. Fails soft: a streaks error should
// not take the rest of the page down, so it renders nothing instead.
export default function StreakCards() {
  const [streaks, setStreaks] = useState(null)

  useEffect(() => {
    getStreaks().then(setStreaks).catch(() => setStreaks([]))
  }, [])

  if (!streaks || streaks.length === 0) return null

  return (
    <div className="section-gap">
      <h2 className="trend-chart-title">Streaks</h2>
      <div className="streak-list">
        {streaks.map((s) => (
          <Link key={s.type} to={`/streaks/${s.type}`} className="streak-card">
            <span className="streak-label">{s.label}</span>
            <div className="streak-numbers">
              <div className="streak-stat">
                <span className="streak-value">{s.currentWeeks}</span>
                <span className="streak-stat-label">current {s.currentWeeks === 1 ? 'week' : 'weeks'}</span>
              </div>
              <div className="streak-stat">
                <span className="streak-value">{s.bestWeeks}</span>
                <span className="streak-stat-label">best {s.bestWeeks === 1 ? 'week' : 'weeks'}</span>
              </div>
              <ChevronRightIcon />
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
