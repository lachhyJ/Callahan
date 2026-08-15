import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getStreakDetail } from '../api/client'
import { CheckIcon } from '../icons'
import SessionList from '../components/SessionList'

function formatWeekLabel(iso) {
  const start = new Date(`${iso}T00:00:00`)
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return `${start.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${end.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`
}

export default function StreakDetailPage() {
  const { type } = useParams()
  const [detail, setDetail] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    setDetail(null)
    setError(null)
    getStreakDetail(type).then(setDetail).catch((err) => setError(err.message))
  }, [type])

  return (
    <main className="page">
      {error && <p className="error">{error}</p>}
      {!error && !detail && <p>Loading…</p>}

      {detail && (
        <>
          <h1>{detail.label}</h1>

          {detail.weeks.length === 0 && (
            <div className="empty-state">
              <p>No sessions logged yet — this fills in once you start training.</p>
              <Link to="/" className="custom-workout-link">Start a workout</Link>
            </div>
          )}

          <div className="streak-week-list">
            {detail.weeks.map((w) => (
              <div key={w.weekStart} className={w.qualifies ? 'streak-week qualifies' : 'streak-week'}>
                <div className="streak-week-header">
                  <span>{formatWeekLabel(w.weekStart)}</span>
                  {w.qualifies && <CheckIcon />}
                </div>

                {w.workouts.length === 0 && w.runs.length === 0 && (
                  <p className="streak-week-empty">Nothing logged</p>
                )}

                <SessionList workouts={w.workouts} runs={w.runs} />
              </div>
            ))}
          </div>
        </>
      )}
    </main>
  )
}
