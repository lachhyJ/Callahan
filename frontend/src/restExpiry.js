// Whether a rest has run out, judged on the absolute end time with no rounding.
//
// The tick effects used to test `Math.round((endAt - now) / 1000) <= 0`, which
// calls a rest over up to 0.5s early. On native the beep starts ~0.15s before
// endAt, so a tick landing in that gap cleared the rest, which cancelled the
// native beep before it began sounding: a silent miss. Exact comparison means
// the rest is only cleared once the beep is already sounding (or has sounded).
export function isRestOver(endAt, nowMs) {
  return endAt <= nowMs
}
