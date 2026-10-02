// Stale-while-revalidate cache for launch-critical GET calls. Paints a page
// instantly from the previous session's response, then always fetches fresh
// data in the background and hands it back too — the network round-trip
// (Cloudflare tunnel + cold WKWebView boot) is the measured launch-latency
// cost, not query time, so removing the *wait* is what actually helps.
//
// Bumped whenever a cached DTO's shape changes, so an old cached response
// from before a deploy is never handed to code that no longer expects it.
const CACHE_VERSION = 'v1'
const PREFIX = `callahan.swr.${CACHE_VERSION}.`

function readCache(key) {
  try {
    const raw = localStorage.getItem(PREFIX + key)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

function writeCache(key, data) {
  try {
    localStorage.setItem(PREFIX + key, JSON.stringify({ data, ts: Date.now() }))
  } catch {
    // storage unavailable/full - the page still works, just without the
    // instant-paint fast path next time
  }
}

// The last cached value for `key`, or null, read synchronously. For seeding
// initial React state so a cached view renders on the very first frame instead
// of waiting for an effect: staleWhileRevalidate called from an effect (or an
// idle callback) paints one render late, which for a card that sits above other
// content shows as a pop-in and a layout shift.
export function peekCache(key) {
  return readCache(key)?.data ?? null
}

// Called on logout, so a later login on the same device never briefly
// paints the previous session's data before its own fetch lands.
export function clearAllCache() {
  try {
    for (const k of Object.keys(localStorage)) {
      if (k.startsWith(PREFIX)) localStorage.removeItem(k)
    }
  } catch {
    // storage unavailable - nothing to clear
  }
}

// Calls onData synchronously with the last cached value (if any) so the page
// can paint before the network round-trip even starts, then always runs
// `fetcher` and calls onData again with the live result once it resolves.
// If the background fetch fails but a cached value already painted, the
// failure is swallowed (logged only) rather than blanking a working view -
// a transient revalidate failure shouldn't be worse than not caching at all.
// Returns the underlying fetch promise so callers can still combine it with
// other fetches (Promise.all) for aggregate error handling / instrumentation.
export function staleWhileRevalidate(key, fetcher, onData) {
  const cached = readCache(key)
  if (cached) onData(cached.data)

  return fetcher()
    .then((data) => {
      writeCache(key, data)
      onData(data)
      return data
    })
    .catch((err) => {
      if (cached) {
        console.warn(`[swr] background revalidate failed for "${key}", keeping cached data:`, err)
        return cached.data
      }
      throw err
    })
}
