import { isoDate } from './dateUtils'

// How far back the status band reaches, on /wellness and the dashboard card
// alike - the 12 weeks Garmin Connect shows, so the band reads the same everywhere.
export const STATUS_BAND_DAYS = 84

// swrCache key for the wellness rows behind the band, shared by the dashboard
// card and /wellness so opening either paints from the other's last fetch. The
// key is fixed (not built from the date range): a range key changes every day,
// so the first open of each day had nothing cached and the band drew in from
// empty. The window length is in the key so changing it can't serve a
// wrong-sized cached range.
export const WELLNESS_ROWS_CACHE_KEY = `wellness-rows-${STATUS_BAND_DAYS}d`

export function wellnessRowsCacheKey(days) {
  return `wellness-rows-${days}d`
}

// The ranges /wellness offers for the status band, after Garmin Connect's
// 4w / 12w / 6m (its "Current" is the headline above the band). 6m is 26 weeks.
// The dashboard card stays at STATUS_BAND_DAYS.
export const STATUS_RANGES = [
  { key: '4w', label: '4w', days: 28 },
  { key: '12w', label: '12w', days: STATUS_BAND_DAYS },
  { key: '6m', label: '6m', days: 182 },
]
export const DEFAULT_STATUS_RANGE = '12w'

export function statusRange(key) {
  return STATUS_RANGES.find((r) => r.key === key) ?? STATUS_RANGES.find((r) => r.key === DEFAULT_STATUS_RANGE)
}

// `count` evenly spaced dates across the window, first and last included, for
// the band's axis. Returns fewer when the window has fewer days than ticks.
export function axisTicks(statusDays, count = 4) {
  if (!statusDays?.length) return []
  const n = Math.min(count, statusDays.length)
  if (n === 1) return [statusDays[0].date]
  const last = statusDays.length - 1
  return Array.from({ length: n }, (_, i) => statusDays[Math.round((i * last) / (n - 1))].date)
}

// Garmin's Training Status, keyed by the numeric code the sync stores in
// DailyWellness.TrainingStatusCode. The codes and colours were matched against
// the Garmin Connect 12-week band on a real account (2026-10-02); the phrase
// Garmin sends alongside ("PRODUCTIVE_1") has an undocumented numeric suffix
// and is deliberately not parsed here. `color` is a CSS custom property so the
// palette lives in App.css next to the other wellness styles.
export const TRAINING_STATUS = {
  1: { key: 'detraining', label: 'Detraining', color: 'var(--ts-detraining)' },
  2: { key: 'unproductive', label: 'Unproductive', color: 'var(--ts-unproductive)' },
  3: { key: 'overreaching', label: 'Overreaching', color: 'var(--ts-overreaching)' },
  4: { key: 'maintaining', label: 'Maintaining', color: 'var(--ts-maintaining)' },
  5: { key: 'recovery', label: 'Recovery', color: 'var(--ts-recovery)' },
  6: { key: 'peaking', label: 'Peaking', color: 'var(--ts-peaking)' },
  7: { key: 'productive', label: 'Productive', color: 'var(--ts-productive)' },
  8: { key: 'strained', label: 'Strained', color: 'var(--ts-strained)' },
}

// A code Garmin adds later (or one we haven't seen) renders as a neutral
// "Other" rather than disappearing or throwing.
const UNKNOWN_STATUS = { key: 'other', label: 'Other', color: 'var(--ts-unknown)' }

export function statusMeta(code) {
  return TRAINING_STATUS[code] ?? UNKNOWN_STATUS
}

// Dense per-day status codes for the `days` days ending at `end` (a Date,
// default today), null for a day with no synced status. Same windowing as
// buildDailySeries so the band lines up with the metric charts above/below it.
export function buildStatusDays(rows, days, end = new Date()) {
  const start = new Date(end)
  start.setDate(start.getDate() - (days - 1))
  const byDate = new Map((rows ?? []).map((r) => [r.date, r]))
  const out = []
  for (let i = 0; i < days; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const date = isoDate(d)
    out.push({ date, code: byDate.get(date)?.trainingStatusCode ?? null })
  }
  return out
}

// Collapse consecutive days with the same code into runs, the way Garmin
// Connect's dated list does. Computed here from the per-day codes rather than
// read from Garmin's own `sinceDate`, which was observed to reset mid-stretch
// (it said 7 Sep for a Recovery stretch Connect shows as starting 3 Sep).
// A null-code stretch is kept as a run with code null so the band can draw a
// gap with the right width.
export function buildStatusRuns(statusDays) {
  const runs = []
  for (const { date, code } of statusDays) {
    const last = runs[runs.length - 1]
    if (last && last.code === code) {
      last.end = date
      last.days += 1
    } else {
      runs.push({ code, start: date, end: date, days: 1 })
    }
  }
  return runs
}

// The most recent day that has a status, with its ACWR figures, for the
// headline above the band. Returns null when no day in `rows` has one.
export function latestStatusRow(rows) {
  let best = null
  for (const r of rows ?? []) {
    if (r.trainingStatusCode == null) continue
    if (best == null || r.date > best.date) best = r
  }
  return best
}

export function formatAcwr(row) {
  if (!row || row.acwrRatio == null) return null
  const parts = [`ratio ${row.acwrRatio.toFixed(1)}`]
  if (row.acuteLoad != null) parts.push(`acute ${row.acuteLoad}`)
  if (row.chronicLoad != null) parts.push(`chronic ${row.chronicLoad}`)
  return parts.join(' · ')
}
