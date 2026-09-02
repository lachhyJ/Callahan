import { recordUsage } from './api/client'

// Route patterns, so /exercises/17 and /exercises/23 aggregate as one screen
// rather than splitting into a row each. Ordered: the first match wins, so
// literal routes must precede the parameterised ones they'd otherwise match
// (/workout/custom before /workout/:templateId).
const ROUTE_PATTERNS = [
  [/^\/workout\/custom$/, '/workout/custom'],
  [/^\/workout\/[^/]+$/, '/workout/:templateId'],
  [/^\/exercises\/[^/]+$/, '/exercises/:exerciseId'],
  [/^\/sessions\/[^/]+$/, '/sessions/:sessionId'],
  [/^\/activities\/[^/]+$/, '/activities/:activityId'],
  [/^\/tournaments\/[^/]+$/, '/tournaments/:tournamentId'],
  [/^\/streaks\/[^/]+$/, '/streaks/:type'],
  [/^\/reports\/[^/]+\/[^/]+$/, '/reports/:year/:month'],
]

export function normalisePath(pathname) {
  if (!pathname) return '/'
  // Trailing slash is noise; "/" itself must survive it.
  const clean = pathname.length > 1 ? pathname.replace(/\/+$/, '') : pathname
  for (const [pattern, normalised] of ROUTE_PATTERNS) {
    if (pattern.test(clean)) return normalised
  }
  return clean
}

// Buffered rather than one request per navigation: a burst of taps through a
// drill-down becomes one POST, and nothing blocks a route change on a network
// call.
const FLUSH_DELAY_MS = 2000

let buffer = []
let flushTimer = null
let currentPath = null

// Dwell is foreground time only. Without this, leaving the app open on the
// dashboard overnight would record a 9-hour "read" of it, and the whole point
// of the number is distinguishing a screen that gets read from one that gets
// bounced off.
let foregroundMs = 0
let enteredAt = null

function currentDwellMs() {
  return Math.round(foregroundMs + (enteredAt === null ? 0 : Date.now() - enteredAt))
}

function scheduleFlush() {
  if (flushTimer !== null) return
  flushTimer = setTimeout(() => {
    flushTimer = null
    flush()
  }, FLUSH_DELAY_MS)
}

export function flush() {
  if (buffer.length === 0) return
  const now = Date.now()
  const events = buffer.map((e) => ({
    kind: e.kind,
    path: e.path,
    fromPath: e.fromPath ?? null,
    dwellMs: e.dwellMs ?? null,
    action: e.action ?? null,
    detail: e.detail ?? null,
    ageMs: now - e.at,
  }))
  buffer = []
  recordUsage(events)
}

export function trackRoute(pathname) {
  const path = normalisePath(pathname)
  if (path === currentPath) return

  buffer.push({
    kind: 'route',
    path,
    fromPath: currentPath,
    dwellMs: currentPath === null ? null : currentDwellMs(),
    at: Date.now(),
  })

  currentPath = path
  foregroundMs = 0
  enteredAt = Date.now()
  scheduleFlush()
}

// Interactions worth separating from plain navigation — mostly "which way did
// you get there", which a route event alone can't distinguish (the dashboard
// links to /trends from both a quick-link tile and, historically, elsewhere).
export function trackAction(action, detail = null) {
  buffer.push({ kind: 'action', path: currentPath ?? '/', action, detail, at: Date.now() })
  scheduleFlush()
}

// Called once from App. Pauses the dwell clock while the tab is hidden and
// flushes on the way out, since a PWA is usually backgrounded rather than
// closed and an un-flushed buffer would otherwise be lost.
export function startUsageTracking() {
  const onVisibility = () => {
    if (document.visibilityState === 'hidden') {
      if (enteredAt !== null) {
        foregroundMs += Date.now() - enteredAt
        enteredAt = null
      }
      flush()
    } else if (enteredAt === null) {
      enteredAt = Date.now()
    }
  }
  document.addEventListener('visibilitychange', onVisibility)
  window.addEventListener('pagehide', flush)
  return () => {
    document.removeEventListener('visibilitychange', onVisibility)
    window.removeEventListener('pagehide', flush)
  }
}

// Test seam: the module holds cross-navigation state by design.
export function __resetUsageForTests() {
  buffer = []
  currentPath = null
  foregroundMs = 0
  enteredAt = null
  if (flushTimer !== null) { clearTimeout(flushTimer); flushTimer = null }
}
