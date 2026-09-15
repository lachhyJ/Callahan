import { useEffect, useState } from 'react'
import { Capacitor } from '@capacitor/core'

// Tracks how far the on-screen keyboard has pushed up from the bottom of the
// layout viewport, so a bottom-docked toolbar/sheet can lift clear of it.
//
// Native (Capacitor iOS): the WKWebView's own visualViewport reporting is
// unreliable here — on device it sometimes leaves a gap between our UI and
// the real keyboard (Capacitor's own native input-accessory bar, the
// up/down/done row, isn't accounted for consistently), which shows up as the
// app's own bottom nav peeking through between the toolbar/sheet and the
// keyboard, by an amount that varies per keyboard transition. The
// @capacitor/keyboard plugin instead reports the OS's own keyboard height
// directly, so use it whenever running natively.
//
// Web/PWA: no native plugin available, so visualViewport (accurate there)
// stays the fallback.
export function useKeyboardInset() {
  const [inset, setInset] = useState(0)

  useEffect(() => {
    if (Capacitor.isNativePlatform()) {
      let cancelled = false
      let showHandle = null
      let hideHandle = null
      import('@capacitor/keyboard').then(({ Keyboard }) => {
        if (cancelled) return
        Keyboard.addListener('keyboardWillShow', (info) => setInset(info.keyboardHeight)).then((h) => {
          if (cancelled) h.remove()
          else showHandle = h
        })
        Keyboard.addListener('keyboardWillHide', () => setInset(0)).then((h) => {
          if (cancelled) h.remove()
          else hideHandle = h
        })
      })
      return () => {
        cancelled = true
        showHandle?.remove()
        hideHandle?.remove()
      }
    }

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
