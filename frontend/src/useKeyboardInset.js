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
export function useKeyboardInset() {
  const [inset, setInset] = useState(0)

  useEffect(() => {
    const vv = window.visualViewport
    if (!vv) return
    function update() {
      setInset(Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop)))
    }
    update()
    vv.addEventListener('resize', update)
    vv.addEventListener('scroll', update)
    return () => {
      vv.removeEventListener('resize', update)
      vv.removeEventListener('scroll', update)
    }
  }, [])

  return inset
}
