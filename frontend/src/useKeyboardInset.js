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
// The "value right, paint wrong" chase that led here (2026-09-15/16,
// on-device each time) turned out to have a real root cause found later in
// useLockDocumentScroll below: a phantom window-level scroll iOS forces
// regardless of the app's own .app-content-only-scrolls architecture. Once
// that's neutralized at the source there's no remaining paint-lag to guard
// against — a settle debounce (below) is still worth keeping so the
// *value* isn't read mid-animation, but hiding the element while unsettled
// was tried at several different trigger scopes (every focus change, every
// .app-content scroll including our own scrollIntoView calls, then only a
// genuine visualViewport change) and produced a visible flicker at every
// scope tried, for a paint-correctness problem that no longer exists —
// removed entirely 2026-09-17 rather than keep narrowing the trigger.
const SETTLE_DELAY_MS = 150

// iOS draws a native input-accessory bar (the ‹ › and Done row) directly
// above the system keyboard for text/number fields — it isn't part of the
// keyboard's own reported height and nothing in the DOM can measure it, so
// a fixed-position element positioned right at the resized viewport's edge
// still renders underneath it. This is a hand-measured estimate, not a
// queryable constant; nudge it if it drifts on a future iOS version — 50
// left a sliver of the toolbar under the accessory bar on-device
// (2026-09-16 morning), 68 overcorrected once the real scroll-drift bug
// (see useLockDocumentScroll) was fixed and stopped compounding the error
// (2026-09-16 afternoon), 58 and then 24 were both still visibly too much
// (2026-09-17). Add it on top of the inset whenever positioning something
// while a text field is actually focused (not when merely showing at the
// screen's resting bottom with no keyboard up at all).
export const KEYBOARD_ACCESSORY_HEIGHT = 0

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

export function useKeyboardInset() {
  const [inset, setInset] = useState(0)

  useEffect(() => {
    const vv = window.visualViewport
    if (!vv) return
    let settleTimer = null

    function compute() {
      return Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop))
    }

    function onViewportChange() {
      setInset(compute())
      clearTimeout(settleTimer)
      settleTimer = setTimeout(() => setInset(compute()), SETTLE_DELAY_MS)
    }

    setInset(compute())
    vv.addEventListener('resize', onViewportChange)
    vv.addEventListener('scroll', onViewportChange)
    return () => {
      clearTimeout(settleTimer)
      vv.removeEventListener('resize', onViewportChange)
      vv.removeEventListener('scroll', onViewportChange)
    }
  }, [])

  return inset
}

// The flip side of useLockDocumentScroll: by snapping window.scrollY back to
// 0 it also cancels iOS's own "scroll the focused field above the keyboard"
// pan, and nothing else scrolls #app-scroll to compensate. The active
// workout's set inputs do it themselves (scrollToSetRow); every other form —
// Tapering, Log activity, Quotes, Login, … — left the field behind the
// keyboard (reported 2026-11-06 on Tapering). This restores that behaviour
// app-wide, scrolling only the app's own scroller and only when the field is
// actually obscured, so pages that already position their own fields no-op.
const FIELD_MARGIN_PX = 24

function isTextEntry(el) {
  if (!el) return false
  if (el.tagName === 'TEXTAREA' || el.isContentEditable) return true
  if (el.tagName !== 'INPUT') return false
  return !['checkbox', 'radio', 'button', 'submit', 'reset', 'range', 'color', 'file', 'hidden'].includes(el.type)
}

export function useScrollFocusedFieldIntoView() {
  useEffect(() => {
    let timer = null

    function ensureVisible() {
      const el = document.activeElement
      if (!isTextEntry(el)) return
      const scroller = document.getElementById('app-scroll')
      // Fields in fixed sheets (plate calc, add-exercise) lift themselves.
      if (!scroller || !scroller.contains(el)) return
      const vv = window.visualViewport
      const visibleBottom = Math.min(
        scroller.getBoundingClientRect().bottom,
        vv ? vv.height + vv.offsetTop : window.innerHeight,
      )
      const visibleTop = scroller.getBoundingClientRect().top
      const r = el.getBoundingClientRect()
      let delta = 0
      if (r.bottom > visibleBottom - FIELD_MARGIN_PX) delta = r.bottom - visibleBottom + FIELD_MARGIN_PX
      else if (r.top < visibleTop + FIELD_MARGIN_PX) delta = r.top - visibleTop - FIELD_MARGIN_PX
      if (delta === 0) return
      const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      scroller.scrollBy({ top: delta, behavior: reduceMotion ? 'auto' : 'smooth' })
    }

    // Wait for the keyboard animation / webview resize to settle before
    // measuring, same reasoning as SETTLE_DELAY_MS above.
    function schedule() {
      clearTimeout(timer)
      timer = setTimeout(ensureVisible, SETTLE_DELAY_MS + 100)
    }

    function onFocusIn(e) {
      if (isTextEntry(e.target)) schedule()
    }

    const vv = window.visualViewport
    document.addEventListener('focusin', onFocusIn)
    vv?.addEventListener('resize', schedule)
    window.addEventListener('resize', schedule)
    return () => {
      clearTimeout(timer)
      document.removeEventListener('focusin', onFocusIn)
      vv?.removeEventListener('resize', schedule)
      window.removeEventListener('resize', schedule)
    }
  }, [])
}
