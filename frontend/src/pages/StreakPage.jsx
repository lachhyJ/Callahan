import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getStreaks } from '../api/client'
import { BackIcon, ChevronRightIcon } from '../icons'

export default function StreakPage() {
  const navigate = useNavigate()
  const [streaks, setStreaks] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getStreaks().then(setStreaks).catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>Streaks</h1>

      {error && <p className="error">{error}</p>}
      {!error && streaks === null && <p>Loading…</p>}

      {streaks && (
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
      )}
    </main>
  )
}
