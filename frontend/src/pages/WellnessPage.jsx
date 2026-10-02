import { useEffect, useState } from 'react'
import { getWellness, getWellnessInsight } from '../api/client'
import {
  buildDailySeries,
  DIRECTION_CLASS,
  formatMetricValue,
  MIN_TREND_READINGS,
  WELLNESS_METRICS,
  wellnessRange,
} from '../wellnessMetrics'
import WellnessSparkline from '../components/WellnessSparkline'
import MetricTrendChart from '../components/MetricTrendChart'
import TrainingStatusBand from '../components/TrainingStatusBand'
import { peekCache, staleWhileRevalidate } from '../swrCache'
import {
  DEFAULT_STATUS_RANGE,
  STATUS_BAND_DAYS,
  STATUS_RANGES,
  statusRange,
  WELLNESS_ROWS_CACHE_KEY,
  wellnessRowsCacheKey,
} from '../trainingStatus'

// The metric charts and sparklines plot the last 12 weeks; the status band can
// reach further back (its longest range), so one fetch covers the longer of the two.
const HISTORY_DAYS = STATUS_BAND_DAYS
const FETCH_DAYS = Math.max(HISTORY_DAYS, ...STATUS_RANGES.map((r) => r.days))
// Own cache key: this page fetches a longer window than the dashboard card, so
// it can't share the card's 84-day key. It still seeds from that key on a cold
// start so arriving from the card paints at once, then upgrades when this lands.
const ROWS_CACHE_KEY = wellnessRowsCacheKey(FETCH_DAYS)
const RANGE_STORAGE_KEY = 'callahan.wellness.statusRange'
const SPARK_DAYS = 28

function storedRangeKey() {
  try {
    return statusRange(localStorage.getItem(RANGE_STORAGE_KEY)).key
  } catch {
    return DEFAULT_STATUS_RANGE
  }
}
// Below this many real readings a metric isn't worth a full trend chart — fall
// back to the compact sparkline (and below the sparkline's own floor, nothing).
const MIN_SPARK_READINGS = 5

const META_BY_KEY = Object.fromEntries(WELLNESS_METRICS.map((m) => [m.key, m]))

export default function WellnessPage() {
  const [insight, setInsight] = useState(null)
  const [error, setError] = useState(null)
  const [loaded, setLoaded] = useState(false)
  const [series, setSeries] = useState(null)
  const [rows, setRows] = useState(() => peekCache(ROWS_CACHE_KEY) ?? peekCache(WELLNESS_ROWS_CACHE_KEY))
  const [seriesState, setSeriesState] = useState('loading') // loading | ready | error
  const [rangeKey, setRangeKey] = useState(storedRangeKey)

  const chooseRange = (key) => {
    setRangeKey(key)
    try {
      localStorage.setItem(RANGE_STORAGE_KEY, key)
    } catch {
      // storage unavailable - the choice just won't survive a reload
    }
  }

  useEffect(() => {
    // Same key as the dashboard card, so arriving from it paints instantly.
    staleWhileRevalidate('wellness-insight', getWellnessInsight, (data) => {
      setInsight(data)
      setLoaded(true)
    }).catch((err) => setError(err.message))
  }, [])

  useEffect(() => {
    const { start, end } = wellnessRange(FETCH_DAYS)
    staleWhileRevalidate(ROWS_CACHE_KEY, () => getWellness(start, end), (data) => {
      setRows(data)
      setSeries(buildDailySeries(data, HISTORY_DAYS))
      setSeriesState('ready')
    }).catch(() => setSeriesState('error'))
  }, [])

  return (
    <main className="page">
      <h1>Wellness</h1>

      {rows && (
        <TrainingStatusBand
          rows={rows}
          days={statusRange(rangeKey).days}
          headExtra={
            <div className="training-status-range" role="group" aria-label="Training status range">
              {STATUS_RANGES.map((r) => (
                <button
                  key={r.key}
                  type="button"
                  className={`training-status-range-btn${r.key === rangeKey ? ' active' : ''}`}
                  aria-pressed={r.key === rangeKey}
                  onClick={() => chooseRange(r.key)}
                >
                  {r.label}
                </button>
              ))}
            </div>
          }
        />
      )}

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

          <p className="page-subtitle wellness-list-caption">
            Each chart is the last 12 weeks. The dashed line is your recent baseline.
          </p>

          <div className="wellness-metric-list">
            {insight.metrics.map((m) => {
              const meta = META_BY_KEY[m.key]
              const scale = meta?.chartScale ?? 1
              const raw = series?.byKey[m.key] ?? []
              const realCount = raw.filter((v) => v != null).length
              const baselineScaled = m.baselineAvg == null ? null : m.baselineAvg * scale
              const showChart = m.direction !== 'insufficient' && realCount >= MIN_TREND_READINGS
              const showSpark =
                !showChart && m.direction !== 'insufficient' && realCount >= MIN_SPARK_READINGS

              return (
                <div key={m.key} className="wellness-metric-block">
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

                  {showChart && (
                    <MetricTrendChart
                      points={series.dates.map((date, i) => ({
                        date,
                        value: raw[i] == null ? null : raw[i] * scale,
                      }))}
                      baselineAvg={baselineScaled}
                      ariaLabel={`${m.label} over the last 12 weeks`}
                    />
                  )}
                  {showSpark && (
                    <WellnessSparkline values={raw.slice(-SPARK_DAYS)} baselineAvg={m.baselineAvg} />
                  )}
                  {seriesState === 'loading' && m.direction !== 'insufficient' && (
                    <div className="wellness-chart-skeleton" aria-hidden="true" />
                  )}
                </div>
              )
            })}
          </div>

          {seriesState === 'error' && (
            <p className="page-subtitle">Couldn't load trend history — showing today's numbers only.</p>
          )}

          <p className="page-subtitle wellness-disclaimer">
            A read on how today compares to your recent normal — not training advice.
          </p>
        </>
      )}
    </main>
  )
}
