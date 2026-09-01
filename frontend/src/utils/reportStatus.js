// An unlocked monthly report is in one of two states, and they mean
// different things to the reader:
//   - in progress: the month itself hasn't ended, so the numbers are
//     partial by definition (a "0 sessions" headline on the 2nd is not
//     a real verdict).
//   - settling: the month is over but late edits / backfills can still
//     land, so the report keeps recomputing until it locks on day 8 of
//     the following month.
export function reportProgressState(year, month) {
  const now = new Date()
  const currentKey = now.getFullYear() * 12 + now.getMonth()
  const reportKey = year * 12 + (month - 1)
  return reportKey >= currentKey ? 'in-progress' : 'settling'
}

export function reportProgressTag(year, month) {
  return reportProgressState(year, month) === 'in-progress' ? 'in progress' : 'still settling'
}

export function reportProgressNote(year, month) {
  return reportProgressState(year, month) === 'in-progress'
    ? "Month still in progress — these numbers are partial and update as you train."
    : 'Month complete — still reconciling late edits; this report locks on day 8.'
}
