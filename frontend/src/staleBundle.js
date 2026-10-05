// Self-heal for stale bundles. The backend's build version is a fresh GUID per
// process start, so it changes on every deploy; a bundle built before the current
// backend may call routes that no longer exist, or just lack recent features.
//
// Two checks, one function:
// - first run on a page load compares against the version this browser last saw
//   (localStorage), which catches a stale index.html served from cache;
// - every later run compares against the version this page load first saw, which
//   catches a webview that was suspended and resumed. A resumed webview keeps its
//   in-memory JS and never remounts, so a check that only ran at mount left the
//   phone on a days-old bundle after several deploys (seen 2026-10-05: footer
//   read an old commit while the server was current).
//
// A resume-time reload is skipped while a workout is in progress: the reload
// would land mid-session on a backgrounded-and-returned app. The next resume
// after the workout ends picks the update up.

export const BUILD_VERSION_KEY = 'callahan_build_version'

export function createStaleBundleCheck({ getHealth, storage, loadActiveWorkout, reload }) {
  let loadedVersion = null

  return async function check() {
    let version
    try {
      ;({ version } = await getHealth())
    } catch {
      return
    }
    if (!version) return

    if (loadedVersion === null) {
      loadedVersion = version
      const stored = storage.getItem(BUILD_VERSION_KEY)
      storage.setItem(BUILD_VERSION_KEY, version)
      if (stored && stored !== version) reload()
      return
    }

    if (version === loadedVersion) return
    if (loadActiveWorkout()) return
    storage.setItem(BUILD_VERSION_KEY, version)
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
