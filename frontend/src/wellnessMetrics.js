import { isoDate } from './dateUtils'

// Shared metric metadata so the /wellness insight rows, the sparklines, and
// the recovery timeline all agree on labels, units, and which direction counts
// as "good". `key` matches MetricInsightDto.Key from the backend; `field`
// matches DailyWellnessDto (camelCase). `chartScale` converts the stored unit
// to the one the timeline chart plots (sleep is stored in seconds, charted in
// hours); `unit` labels that charted axis.
export const WELLNESS_METRICS = [
  { key: 'readiness', label: 'Readiness', field: 'trainingReadinessScore', unit: 'score', higherIsBetter: true },
  { key: 'sleepScore', label: 'Sleep score', field: 'sleepScore', unit: 'score', higherIsBetter: true },
  { key: 'sleepDuration', label: 'Sleep duration', field: 'sleepSeconds', unit: 'hours', chartScale: 1 / 3600, higherIsBetter: true },
  { key: 'hrv', label: 'HRV', field: 'hrvLastNightAvg', unit: 'ms', higherIsBetter: true },
  { key: 'restingHeartRate', label: 'Resting HR', field: 'restingHeartRate', unit: 'bpm', higherIsBetter: false },
]

// Recovery-direction tint classes, matching the LiftTrendsList up/down convention.
export const DIRECTION_CLASS = { below: 'down', above: 'up' }

export function formatMetricValue(key, value) {
  if (value == null) return '—'
  if (key === 'sleepDuration') {
    const h = Math.floor(value / 3600)
    const m = Math.round((value % 3600) / 60)
    return `${h}h ${m}m`
  }
  if (key === 'hrv') return `${Math.round(value)} ms`
  if (key === 'restingHeartRate') return `${Math.round(value)} bpm`
  return Math.round(value)
}

// The date range to request from GET /api/wellness for a `days`-day window
// ending today.
export function wellnessRange(days) {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - (days - 1))
  return { start: isoDate(start), end: isoDate(end) }
}

// Turn the sparse daily rows from GET /api/wellness into dense per-metric
// arrays (null for any missing day) covering the `days` days ending today, plus
// the aligned date list — so a sparkline can break the line on gaps and the
// timeline chart can label its x-axis.
export function buildDailySeries(rows, days) {
  const start = new Date()
  start.setDate(start.getDate() - (days - 1))
  const byDate = new Map((rows ?? []).map((r) => [r.date, r]))
  const dates = []
  const byKey = {}
  for (const meta of WELLNESS_METRICS) byKey[meta.key] = []
  for (let i = 0; i < days; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const iso = isoDate(d)
    dates.push(iso)
    const row = byDate.get(iso)
    for (const meta of WELLNESS_METRICS) {
      byKey[meta.key].push(row && row[meta.field] != null ? row[meta.field] : null)
    }
  }
  return { dates, byKey }
}
