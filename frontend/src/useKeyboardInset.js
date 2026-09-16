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
// Still not enough on its own, though (2026-09-15, same day, on-device
// again): a resize/scroll event on visualViewport fires with whatever the
// viewport looks like at that exact instant, and that instant isn't always
// the settled final state — the very first keyboard-show in a session, and
// switching focus from one field to another *without* the keyboard closing
// in between (only a scroll-to-bring-the-new-field-into-view happens, no
// full resize), both fire events mid-animation with a transient, wrong
// value that never got corrected because nothing fired again afterward.
// Re-check shortly after the last event settles, and also after any focus
// change lands on a new element — a focus/blur pair doesn't guarantee a
// visualViewport event at all if the OS doesn't need to scroll further.
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

export function useKeyboardInset() {
  const [inset, setInset] = useState(0)

  useEffect(() => {
    const vv = window.visualViewport
    if (!vv) return
    let settleTimer = null

    function compute() {
      return Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop))
    }

    function scheduleSettleCheck() {
      clearTimeout(settleTimer)
      settleTimer = setTimeout(() => setInset(compute()), SETTLE_DELAY_MS)
    }

    function update() {
      setInset(compute())
      scheduleSettleCheck()
    }

    function onFocusChange() {
      scheduleSettleCheck()
    }

    update()
    vv.addEventListener('resize', update)
    vv.addEventListener('scroll', update)
    document.addEventListener('focusin', onFocusChange)
    document.addEventListener('focusout', onFocusChange)
    return () => {
      clearTimeout(settleTimer)
      vv.removeEventListener('resize', update)
      vv.removeEventListener('scroll', update)
      document.removeEventListener('focusin', onFocusChange)
      document.removeEventListener('focusout', onFocusChange)
    }
  }, [])

  return inset
}
