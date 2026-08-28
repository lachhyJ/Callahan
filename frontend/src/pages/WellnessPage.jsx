import { useEffect, useState } from 'react'
import { getWellness, getWellnessInsight } from '../api/client'
import { buildDailySeries, DIRECTION_CLASS, formatMetricValue, wellnessRange } from '../wellnessMetrics'
import WellnessSparkline from '../components/WellnessSparkline'

// Days of history the per-metric sparklines cover.
const SPARK_DAYS = 35

export default function WellnessPage() {
  const [insight, setInsight] = useState(null)
  const [error, setError] = useState(null)
  const [loaded, setLoaded] = useState(false)
  const [series, setSeries] = useState(null)

  useEffect(() => {
    getWellnessInsight()
      .then((data) => {
        setInsight(data)
        setLoaded(true)
      })
      .catch((err) => setError(err.message))
  }, [])

  useEffect(() => {
    // Additive — a sparkline fetch failure just leaves the numbers as they are.
    const { start, end } = wellnessRange(SPARK_DAYS)
    getWellness(start, end)
      .then((rows) => setSeries(buildDailySeries(rows, SPARK_DAYS)))
      .catch(() => {})
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
                <div className="wellness-metric-row-main">
                  <div className="wellness-metric-name">
                    <span>{m.label}</span>
                    <span className="wellness-metric-phrase">{m.phrase}</span>
                  </div>
                  <div className="wellness-metric-values">
                    <span className={`wellness-metric-today ${DIRECTION_CLASS[m.direction] ?? ''}`}>
                      {formatMetricValue(m.key, m.today)}
                    </span>
                    {m.direction !== 'insufficient' && (
                      <span className="wellness-metric-baseline">
                        typically ~{formatMetricValue(m.key, m.baselineAvg)}
                      </span>
                    )}
                  </div>
                </div>
                {series && m.direction !== 'insufficient' && (
                  <WellnessSparkline values={series[m.key]} baselineAvg={m.baselineAvg} />
                )}
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
