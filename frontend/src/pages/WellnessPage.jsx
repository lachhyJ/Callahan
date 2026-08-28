import { useEffect, useState } from 'react'
import { getWellnessInsight } from '../api/client'

function formatValue(key, value) {
  if (value == null) return '—'
  if (key === 'sleepDuration') {
    const h = Math.floor(value / 3600)
    const m = Math.round((value % 3600) / 60)
    return `${h}h ${m}m`
  }
  if (key === 'hrv') return `${Math.round(value)} ms`
  return Math.round(value)
}

// "below" reads as a warning tint, "above" as positive - matches the
// LiftTrendsList up/down convention. in_line / insufficient stay neutral.
const DIRECTION_CLASS = { below: 'down', above: 'up' }

export default function WellnessPage() {
  const [insight, setInsight] = useState(null)
  const [error, setError] = useState(null)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    getWellnessInsight()
      .then((data) => {
        setInsight(data)
        setLoaded(true)
      })
      .catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      <h1>Wellness</h1>

      {error && <p className="error">{error}</p>}
      {!error && !loaded && <p>Loading…</p>}

      {loaded && !insight && (
        <p className="page-subtitle">No wellness data synced in the last few days.</p>
      )}

      {insight && !insight.hasEnoughHistory && (
        <p className="page-subtitle">
          Not enough wellness history yet — check back after a week or so of syncs.
        </p>
      )}

      {insight && insight.hasEnoughHistory && (
        <>
          <p className="wellness-headline">{insight.headline}</p>

          <div className="wellness-metric-list">
            {insight.metrics.map((m) => (
              <div key={m.key} className="wellness-metric-row">
                <div className="wellness-metric-name">
                  <span>{m.label}</span>
                  <span className="wellness-metric-phrase">{m.phrase}</span>
                </div>
                <div className="wellness-metric-values">
                  <span className={`wellness-metric-today ${DIRECTION_CLASS[m.direction] ?? ''}`}>
                    {formatValue(m.key, m.today)}
                  </span>
                  {m.direction !== 'insufficient' && (
                    <span className="wellness-metric-baseline">
                      typically ~{formatValue(m.key, m.baselineAvg)}
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>

          <p className="page-subtitle wellness-disclaimer">
            A read on how today compares to your recent normal — not training advice.
          </p>
        </>
      )}
    </main>
  )
}
