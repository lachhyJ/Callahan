import { useEffect, useState } from 'react'

// Tracks how far the on-screen keyboard has pushed up from the bottom of the
// layout viewport, so a bottom-docked toolbar/sheet can lift clear of it.
//
// This used to also try the @capacitor/keyboard plugin's raw keyboardHeight
// on native iOS, on the theory that visualViewport was unreliable there.
// On-device debugging (2026-09-15) proved that theory wrong: iOS's WKWebView
// already auto-resizes itself above the keyboard on its own — confirmed by
// logging window.innerHeight/document.documentElement.clientHeight/
// visualViewport.height together mid-keyboard and finding all three already
// shrunk to the same value, matching (screen height − keyboard height)
// exactly, with no code involved. Adding the plugin's keyboardHeight as a
// *second* offset on top of a webview that had already resized itself by
// that same amount double-counted every time — not intermittently, always —
// which is worse than the original bug. Plain visualViewport self-corrects
// to ~0 in that already-resized case (innerHeight and vv.height end up
// equal), so it's the only offset ever safely computed.
//
// Still not enough on its own (2026-09-15/16, on-device each time): the
// *value* can be right while the *paint* is still wrong. iOS composites
// `position: fixed` elements as part of the scrolling content while a
// scroll is actively animating — whether that's the browser's own
// "scroll the focused field into view" pass, or our own explicit
// scrollIntoView call — and only re-pins them to the viewport a beat after
// scrolling truly stops. Portalling to <body> (matching the fix ConfirmSheet
// needed for a *different* problem, a stacking-context trap) does not touch
// this at all — confirmed on-device by finding this hook's own debug overlay
// missing its first few lines, scrolled out of frame by the exact same
// amount as the page's scrollY, despite being position:fixed at top:0 and
// portaled to body itself. There is no CSS fix for this; the only reliable
// mitigation is to not let anything see the mid-scroll paint at all — hide
// the toolbar/sheet for the duration of any scroll (however it was
// triggered) and reveal it only once nothing has moved for a beat.
const SETTLE_DELAY_MS = 150

// iOS draws a native input-accessory bar (the ‹ › and Done row) directly
// above the system keyboard for text/number fields — it isn't part of the
// keyboard's own reported height and nothing in the DOM can measure it, so
// a fixed-position element positioned right at the resized viewport's edge
// still renders underneath it. This is a hand-measured estimate, not a
// queryable constant; nudge it if it drifts on a future iOS version — the
// first guess (50) still left a sliver of the toolbar under the accessory
// bar on-device (2026-09-16), so this includes a bit of headroom rather
// than the bar's exact measured height, on the theory that a few px of gap
// above the bar reads better than a few px still hidden under it. Add it on
// top of the inset whenever positioning something while a text field is
// actually focused (not when merely showing at the screen's resting bottom
// with no keyboard up at all).
export const KEYBOARD_ACCESSORY_HEIGHT = 68

// .app-content is documented (App.css) as the app's one and only scrolling
// element — #root itself locks height:100svh/overflow:hidden specifically
// so nothing above .app-content ever needs to move. iOS's native "scroll
// the focused input into view" pass ignores that entirely: it operates on
// the WKWebView's own backing scroll view, a layer below the DOM/CSS model,
// and pans the *whole rendered page* (a real, persistent window.scrollY —
// confirmed on-device 2026-09-16, not a transient animation frame) to help
// bring a focused field above the keyboard, even though .app-content's own
// internal scroll already handles that. Once the page is panned this way,
// every position:fixed element — including ones portaled straight to
// <body> — renders shifted by that same amount, because iOS composites
// them against the panned page, not the true viewport. No CSS fixes this;
// pinning html/body with position:fixed was tried and did not stop the
// pan (also confirmed on-device). The only thing that works is snapping
// scrollY back to 0 the moment it drifts, so the pan never has anywhere to
// settle other than back where it started — call this once, near the app
// root, for the lifetime of the app.
export function useLockDocumentScroll() {
  useEffect(() => {
    function resetScroll() {
      if (window.scrollY !== 0 || window.scrollX !== 0) window.scrollTo(0, 0)
    }
    window.addEventListener('scroll', resetScroll, { passive: true })
    return () => window.removeEventListener('scroll', resetScroll)
  }, [])
}

// Returns { inset, unsettled }. `unsettled` is true for a brief window
// around any scroll, resize, or focus change — every consumer should hide
// itself (not just reposition) while this is true, since the *position* can
// be numerically correct and still paint in the wrong place until settled.
export function useKeyboardInset() {
  const [inset, setInset] = useState(0)
  const [unsettled, setUnsettled] = useState(false)

  useEffect(() => {
    const vv = window.visualViewport
    if (!vv) return
    let settleTimer = null

    function compute() {
      return Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop))
    }

    function markUnsettled() {
      setUnsettled(true)
      clearTimeout(settleTimer)
      settleTimer = setTimeout(() => {
        setInset(compute())
        setUnsettled(false)
      }, SETTLE_DELAY_MS)
    }

    function onViewportChange() {
      setInset(compute())
      markUnsettled()
    }

    setInset(compute())
    vv.addEventListener('resize', onViewportChange)
    vv.addEventListener('scroll', onViewportChange)
    document.addEventListener('focusin', markUnsettled)
    document.addEventListener('focusout', markUnsettled)
    // `capture: true` so this also fires for a scroll on any descendant
    // scrollable (e.g. .app-content), not just window/document itself —
    // scroll events don't bubble, only capture.
    document.addEventListener('scroll', markUnsettled, { capture: true, passive: true })
    return () => {
      clearTimeout(settleTimer)
      vv.removeEventListener('resize', onViewportChange)
      vv.removeEventListener('scroll', onViewportChange)
      document.removeEventListener('focusin', markUnsettled)
      document.removeEventListener('focusout', markUnsettled)
      document.removeEventListener('scroll', markUnsettled, { capture: true })
    }
  }, [])

  return { inset, unsettled }
}
