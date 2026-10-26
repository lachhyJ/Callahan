// Lengths for the lock-screen quotes list.
//
// The soft limit is only a warning: the inline lock-screen slot truncates at about
// this many characters, and exactly where depends on the date text beside it, so it
// is calibrated by eye on the phone rather than derived. The hard limit mirrors
// QuotesController.MaxLength and exists to stop a pasted paragraph.
export const QUOTE_SOFT_LIMIT = 24
export const QUOTE_MAX_LENGTH = 200

// Counted in characters as a person sees them, not UTF-16 units, so an emoji or an
// accented letter is one.
const length = (text) => [...text].length

export function isLongQuote(text) {
  return length(text.trim()) > QUOTE_SOFT_LIMIT
}

export function isTooLongQuote(text) {
  return length(text.trim()) > QUOTE_MAX_LENGTH
}

// A pasted list is one quote per line. Blank lines are dropped here; duplicates are
// left for the server, which knows what is already saved.
export function splitQuoteLines(text) {
  return text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
}

// Newest first, like the server's list.
export function sortQuotes(quotes) {
  return [...quotes].sort((a, b) => b.createdAt.localeCompare(a.createdAt) || b.id - a.id)
}

// What the page says after an add: how many went in and what was left out.
export function addSummary(result) {
  const added = result.quotes.length
  const reasons = {}
  for (const s of result.skipped) reasons[s.reason] = (reasons[s.reason] ?? 0) + 1
  const parts = []
  if (added > 0) parts.push(`Added ${added}`)
  for (const [reason, count] of Object.entries(reasons)) parts.push(`${count} ${reason}`)
  return parts.length > 0 ? `${parts.join(', ')}.` : ''
}
