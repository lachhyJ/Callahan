import { useEffect, useLayoutEffect, useRef } from 'react'
import { useLocation, useNavigationType } from 'react-router-dom'

// #app-scroll is the only scroller (see .app-content), so the browser's own
// scroll restoration has nothing to restore. Remember scrollTop per history
// entry and put it back on a POP (back / forward). Pages fetch their data
// after mounting, so the content is usually too short to scroll to the saved
// offset at first; keep retrying until it's tall enough or a short deadline
// passes, and stand down if the user scrolls in the meantime.
const RESTORE_WINDOW_MS = 2000

export function useScrollRestoration() {
  const location = useLocation()
  const navType = useNavigationType()
  const positions = useRef(new Map())
  const keyRef = useRef(location.key)
  // Updated during render, i.e. before the scroll event the new route's
  // layout clamp may fire, so a clamped value is never filed under the old key.
  keyRef.current = location.key

  useEffect(() => {
    const el = document.getElementById('app-scroll')
    if (!el) return undefined
    const onScroll = () => positions.current.set(keyRef.current, el.scrollTop)
    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [])

  useLayoutEffect(() => {
    if (navType !== 'POP') return undefined
    const el = document.getElementById('app-scroll')
    const target = positions.current.get(location.key)
    if (!el || !target) return undefined

    const deadline = performance.now() + RESTORE_WINDOW_MS
    let frame = 0
    let lastSet = null
    const cancel = () => { cancelAnimationFrame(frame); frame = 0 }
    const tryRestore = () => {
      // The user scrolled away from where we put it: leave them be.
      if (lastSet !== null && Math.abs(el.scrollTop - lastSet) > 2) return
      el.scrollTop = target
      lastSet = el.scrollTop
      if (lastSet >= target - 1 || performance.now() > deadline) return
      frame = requestAnimationFrame(tryRestore)
    }
    el.addEventListener('touchstart', cancel, { passive: true, once: true })
    el.addEventListener('wheel', cancel, { passive: true, once: true })
    tryRestore()
    return () => {
      cancel()
      el.removeEventListener('touchstart', cancel)
      el.removeEventListener('wheel', cancel)
    }
  }, [location.key, navType])
}
