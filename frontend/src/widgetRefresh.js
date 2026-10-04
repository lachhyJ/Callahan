import { Capacitor, registerPlugin } from '@capacitor/core'

// Asks the native home screen widgets to reload now, instead of waiting for their
// hourly timeline. The widgets are separate processes with no shared storage (no
// App Group on free provisioning), but the app can still tell WidgetKit to reload
// them; each then refetches its own data. See WidgetBridgePlugin.swift.
//
// Native only: the web build has no widgets, so this is a no-op there. It also
// swallows every failure, because an installed build older than this plugin simply
// does not have a `reload` method, and a widget refresh must never break the
// action that triggered it.

const WidgetBridge = registerPlugin('WidgetBridge')

// A burst of writes (ticking several sets, logging then syncing) should reload the
// widgets once, after the burst settles.
const DEBOUNCE_MS = 2000

// The injectable core, so the debounce and the failure handling can be tested
// without a native bridge or real timers.
export function createWidgetRefresher({
  isNative,
  reload,
  delayMs = DEBOUNCE_MS,
  setTimer = setTimeout,
  clearTimer = clearTimeout,
}) {
  let pending = null

  return function refresh() {
    if (!isNative) return
    if (pending !== null) clearTimer(pending)
    pending = setTimer(() => {
      pending = null
      // `then` so a synchronous throw from reload is caught the same way as a
      // rejected promise.
      Promise.resolve()
        .then(reload)
        .catch(() => {})
    }, delayMs)
  }
}

export const refreshWidgets = createWidgetRefresher({
  isNative: Capacitor.isNativePlatform(),
  reload: () => WidgetBridge.reload(),
})

// Reload once at launch and again whenever the app comes back to the foreground, so
// opening the app is enough to bring the widgets up to date. Returns a cleanup
// function, like startUsageTracking.
export function startWidgetRefresh() {
  const onVisibility = () => {
    if (document.visibilityState === 'visible') refreshWidgets()
  }
  document.addEventListener('visibilitychange', onVisibility)
  refreshWidgets()
  return () => document.removeEventListener('visibilitychange', onVisibility)
}
