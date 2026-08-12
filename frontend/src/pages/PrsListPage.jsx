import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getExercisePrs } from '../api/client'
import { BackIcon } from '../icons'

function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

export default function PrsListPage() {
  const navigate = useNavigate()
  const [prs, setPrs] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getExercisePrs().then(setPrs).catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>PRs</h1>

      {error && <p className="error">{error}</p>}
      {!error && prs === null && <p>Loading…</p>}

      {prs && prs.length === 0 && (
        <div className="empty-state">
          <p>No PRs yet — log a set to start tracking them.</p>
        </div>
      )}

      {prs && prs.length > 0 && (
        <div className="pr-list">
          {prs.map((pr) => (
            <Link key={pr.exerciseId} to={`/exercises/${pr.exerciseId}`} className="pr-list-item">
              <div className="pr-list-item-name">
                <span>{pr.exerciseName}</span>
                {pr.primaryMuscle && <span className="primary-muscle">{pr.primaryMuscle}</span>}
              </div>
              <div className="pr-list-item-stat">
                <span className="pr-weight">{formatWeight(pr.heaviestWeightKg)} kg</span>
                <span className="pr-date">{pr.heaviestWeightDate}</span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </main>
  )
}
