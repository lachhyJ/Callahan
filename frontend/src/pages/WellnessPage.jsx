import { useEffect, useState } from 'react'
import { getWellness, getWellnessInsight } from '../api/client'
import { buildDailySeries, DIRECTION_CLASS, formatMetricValue, WELLNESS_METRICS, wellnessRange } from '../wellnessMetrics'
import WellnessSparkline from '../components/WellnessSparkline'
import MetricTrendChart from '../components/MetricTrendChart'

// One fetch covers both surfaces: the timeline plots the full window, the
// row sparklines show the last few weeks of it.
const HISTORY_DAYS = 84
const SPARK_DAYS = 28
// A metric needs at least this many real readings in the window before its
// 12-week chart is worth drawing.
const MIN_TIMELINE_POINTS = 10

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
    // Additive — a history fetch failure just leaves the numbers as they are.
    const { start, end } = wellnessRange(HISTORY_DAYS)
    getWellness(start, end)
      .then((rows) => setSeries(buildDailySeries(rows, HISTORY_DAYS)))
      .catch(() => {})
  }, [])

  const baselineFor = (key) => insight?.metrics.find((m) => m.key === key)?.baselineAvg ?? null

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
                  <WellnessSparkline values={series.byKey[m.key].slice(-SPARK_DAYS)} baselineAvg={m.baselineAvg} />
                )}
              </div>
            ))}
          </div>

          {series && (
            <section className="wellness-timeline section-gap">
              <h2>Last 12 weeks</h2>
              {WELLNESS_METRICS.map((meta) => {
                const scale = meta.chartScale ?? 1
                const points = series.dates.map((date, i) => {
                  const raw = series.byKey[meta.key][i]
                  return { date, value: raw == null ? null : raw * scale }
                })
                const realCount = points.filter((p) => p.value != null).length
                const baseline = baselineFor(meta.key)
                return (
                  <div key={meta.key} className="wellness-timeline-chart">
                    <h3 className="trend-chart-title">{meta.label}</h3>
                    {realCount < MIN_TIMELINE_POINTS ? (
                      <p className="page-subtitle">Not enough history yet.</p>
                    ) : (
                      <MetricTrendChart
                        points={points}
                        baselineAvg={baseline == null ? null : baseline * scale}
                        caption={`${meta.unit} — dashed line is your 28-day average`}
                      />
                    )}
                  </div>
                )
              })}
            </section>
          )}

          <p className="page-subtitle wellness-disclaimer">
            A read on how today compares to your recent normal — not training advice.
          </p>
        </>
      )}
    </main>
  )
}
