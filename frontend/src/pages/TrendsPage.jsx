import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getTrends } from '../api/client'
import { BackIcon } from '../icons'
import ConsistencyTrendChart from '../components/ConsistencyTrendChart'
import VolumeTrendChart from '../components/VolumeTrendChart'

export default function TrendsPage() {
  const navigate = useNavigate()
  const [months, setMonths] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getTrends(6).then(setMonths).catch((err) => setError(err.message))
  }, [])

  const hasAnyData = months?.some((m) => m.gymSessions > 0 || m.runSessions > 0)

  return (
    <main className="page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>Trends</h1>

      {error && <p className="error">{error}</p>}
      {!error && months === null && <p>Loading…</p>}

      {months && !hasAnyData && (
        <div className="empty-state">
          <p>Not enough history yet — trends need a few months of sessions to say anything useful.</p>
        </div>
      )}

      {months && hasAnyData && (
        <>
          <ConsistencyTrendChart months={months} />
          <VolumeTrendChart months={months} />
        </>
      )}
    </main>
  )
}
