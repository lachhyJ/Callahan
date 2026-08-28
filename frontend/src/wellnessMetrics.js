import { isoDate } from './dateUtils'

// Shared metric metadata so the /wellness insight rows, the sparklines, and
// (later) the recovery timeline all agree on labels, units, and which
// direction counts as "good". `key` matches MetricInsightDto.Key from the
// backend; `field` matches DailyWellnessDto (camelCase).
export const WELLNESS_METRICS = [
  { key: 'readiness', label: 'Readiness', field: 'trainingReadinessScore', higherIsBetter: true },
  { key: 'sleepScore', label: 'Sleep score', field: 'sleepScore', higherIsBetter: true },
  { key: 'sleepDuration', label: 'Sleep duration', field: 'sleepSeconds', higherIsBetter: true },
  { key: 'hrv', label: 'HRV', field: 'hrvLastNightAvg', higherIsBetter: true },
  { key: 'restingHeartRate', label: 'Resting HR', field: 'restingHeartRate', higherIsBetter: false },
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
// arrays (null for any missing day) covering the `days` days ending today, so
// a sparkline can render gaps as breaks in the line.
export function buildDailySeries(rows, days) {
  const start = new Date()
  start.setDate(start.getDate() - (days - 1))
  const byDate = new Map((rows ?? []).map((r) => [r.date, r]))
  const out = {}
  for (const meta of WELLNESS_METRICS) out[meta.key] = []
  for (let i = 0; i < days; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const row = byDate.get(isoDate(d))
    for (const meta of WELLNESS_METRICS) {
      out[meta.key].push(row && row[meta.field] != null ? row[meta.field] : null)
    }
  }
  return out
}
