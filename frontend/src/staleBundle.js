// Self-heal for stale bundles: reload when the server is serving a newer build
// than the one this page is running.
//
// "Newer" is read from the bundle itself. index.html is served no-cache and
// references the hashed entry script (`assets/index-<hash>.js`), so a changed
// filename means a deploy landed. The backend's per-process version GUID is not
// usable for this: a frontend-only deploy leaves the backend container running,
// so the GUID doesn't change (seen 2026-10-06).
//
// It runs at launch and on every return to the foreground. The foreground check
// is the point: a webview that was suspended and resumed keeps its in-memory JS
// and never remounts, so a launch-only check left the phone on a days-old bundle
// across several deploys (seen 2026-10-05: footer read an old commit while the
// server was current).
//
// A reload is skipped while a workout is in progress, since it would land
// mid-session on a backgrounded-and-returned app. The next resume after the
// workout ends picks the update up.

const ENTRY_RE = /assets\/index-[\w-]+\.js/

export function entryScript(html) {
  const match = ENTRY_RE.exec(html)
  return match ? match[0] : null
}

export function createStaleBundleCheck({ runningEntry, fetchIndex, loadActiveWorkout, reload }) {
  return async function check() {
    if (!runningEntry) return
    let served
    try {
      served = entryScript(await fetchIndex())
    } catch {
      return
    }
    if (!served || served === runningEntry) return
    if (loadActiveWorkout()) return
    reload()
  }
}

// Runs the check at launch and whenever the app returns to the foreground.
// Returns a cleanup function, like startWidgetRefresh.
export function startStaleBundleCheck(deps) {
  const check = createStaleBundleCheck(deps)
  const onVisibility = () => {
    if (document.visibilityState === 'visible') check()
  }
  document.addEventListener('visibilitychange', onVisibility)
  check()
  return () => document.removeEventListener('visibilitychange', onVisibility)
}

// The entry script this page was loaded with. Null in dev (Vite serves
// /src/main.jsx, not a hashed asset), which turns the check off there.
export function runningEntryScript(doc = document) {
  for (const script of doc.querySelectorAll('script[src]')) {
    const match = ENTRY_RE.exec(script.getAttribute('src'))
    if (match) return match[0]
  }
  return null
}
