import { isoDate } from './dateUtils'

// Garmin Training Load Balance: a rolling-window load split into low aerobic,
// high aerobic and anaerobic, each against Garmin's own target range for that
// day. The sync stores it per day on the wellness rows (balance* fields).

// Lowest to highest intensity, the order Garmin Connect draws them.
export const BALANCE_BARS = [
  { key: 'aerobicLow', label: 'Low aerobic', short: 'Low', value: 'balanceAerobicLow', min: 'balanceAerobicLowMin', max: 'balanceAerobicLowMax' },
  { key: 'aerobicHigh', label: 'High aerobic', short: 'High', value: 'balanceAerobicHigh', min: 'balanceAerobicHighMin', max: 'balanceAerobicHighMax' },
  { key: 'anaerobic', label: 'Anaerobic', short: 'Anaerobic', value: 'balanceAnaerobic', min: 'balanceAnaerobicMin', max: 'balanceAnaerobicMax' },
]

// The track runs a little past the larger of the target max and the value, so
// the end of the target zone and an overshoot both stay on screen.
const TRACK_HEADROOM = 1.15

// 'short' | 'on' | 'over', or null when there is no value to place. Colour is
// never the only channel: the tile prints this as a word beside the number.
// A bar whose target minimum is 0 (anaerobic) can only ever be on or over.
export function balanceState(value, min, max) {
  if (value == null || min == null || max == null) return null
  if (value < min) return 'short'
  if (value > max) return 'over'
  return 'on'
}

export const BALANCE_STATE_LABEL = { short: 'Short', on: 'On target', over: 'Over' }

// Geometry for one bar as fractions (0-1) of its track: where the target zone
// starts and ends and how far the value fills. null when any input is missing.
export function balanceBarGeometry(value, min, max) {
  if (value == null || min == null || max == null) return null
  const domain = Math.max(max, value) * TRACK_HEADROOM
  if (domain <= 0) return null
  return { zoneStart: min / domain, zoneEnd: max / domain, fill: value / domain }
}

// The worst state across the three bars, 'short' over 'over' over 'on', for the
// dot beside Garmin's headline. Taken from the numbers, not the phrase, for the
// same reason as the bars: a shortage outranks "focus" in the phrase.
export function overallBalanceState(row) {
  const states = BALANCE_BARS.map((b) => balanceState(row?.[b.value], row?.[b.min], row?.[b.max]))
  if (states.includes('short')) return 'short'
  if (states.includes('over')) return 'over'
  return states.includes('on') ? 'on' : null
}

// The newest row that carries a balance. Any one value counts, so a day with
// only some of the fields still shows what it has.
export function latestBalanceRow(rows) {
  let best = null
  for (const r of rows ?? []) {
    const has = BALANCE_BARS.some((b) => r[b.value] != null)
    if (!has) continue
    if (best == null || r.date > best.date) best = r
  }
  return best
}

// Garmin's feedback phrase ("AEROBIC_HIGH_SHORTAGE", "ANAEROBIC_FOCUS") as
// sentence-case text. Done generically rather than from a table: only two
// phrases have been seen, so an unseen one still reads sensibly. A shortage
// outranks "focus" in the phrase, so it is a label only - the bars carry state.
export function balanceHeadline(phrase) {
  if (!phrase) return null
  const text = phrase.toLowerCase().replace(/_/g, ' ').trim()
  return text ? text.charAt(0).toUpperCase() + text.slice(1) : null
}

// One bar's value and Garmin's target range for each of the `days` days ending
// at `end`, oldest first. A day with no value is a gap (value null); the range
// is carried per day because Garmin moves it with the load, so the target is a
// band that drifts, not a fixed pair of lines. Dates are the stored ISO dates.
export function buildBalanceSeries(rows, bar, days, end = new Date()) {
  const byDate = new Map((rows ?? []).map((r) => [r.date, r]))
  const out = []
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(end)
    d.setDate(d.getDate() - i)
    const date = isoDate(d)
    const row = byDate.get(date)
    const value = row?.[bar.value] ?? null
    out.push({
      date,
      value,
      min: value == null ? null : row[bar.min] ?? null,
      max: value == null ? null : row[bar.max] ?? null,
    })
  }
  return out
}
