import { useCallback, useLayoutEffect, useRef, useState } from 'react'

// The exercise focus cue. A plain <input> scrolled a long cue out of view one
// line at a time — "Minimum ground contact time, not height. Knees near…" with
// no way to read the rest. This is the same borderless italic field but grows
// to fit its text. Keeps the underline-on-focus affordance via .cue-input.
//
// Collapsed to CLAMP_LINES by default so a long cue doesn't push the sets off
// screen; a More/Less toggle shows the rest, and focusing the field to edit
// also expands it. The field never scrolls internally (overflow hidden): a
// scrollable textarea captured the swipe, so scrolling the session with a
// finger that started on a cue went nowhere.
const CLAMP_LINES = 3

export default function CueInput({ value, onChange, onBlur, placeholder, ariaLabel }) {
  const ref = useRef(null)
  const [expanded, setExpanded] = useState(false)
  const [focused, setFocused] = useState(false)
  const [overflowing, setOverflowing] = useState(false)

  const fit = useCallback((el) => {
    if (!el) return
    el.style.height = 'auto'
    const style = getComputedStyle(el)
    const pad = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
    const clampPx = parseFloat(style.lineHeight) * CLAMP_LINES + pad
    const over = el.scrollHeight > clampPx + 1
    setOverflowing(over)
    el.style.height = `${over && !expanded && !focused ? clampPx : el.scrollHeight}px`
  }, [expanded, focused])

  // Re-fit whenever the value changes from outside (initial load, a fetched
  // cue), on every keystroke below, and when the expanded state flips.
  useLayoutEffect(() => {
    fit(ref.current)
  }, [value, fit])

  return (
    <>
      <textarea
        ref={ref}
        rows={1}
        className="cue-input"
        placeholder={placeholder}
        value={value}
        onChange={(e) => {
          onChange(e)
          fit(e.target)
        }}
        onFocus={() => setFocused(true)}
        onBlur={(e) => {
          setFocused(false)
          onBlur?.(e)
        }}
        aria-label={ariaLabel}
      />
      {overflowing && !focused && (
        <button type="button" className="cue-toggle" onClick={() => setExpanded((x) => !x)}>
          {expanded ? 'Less' : 'More'}
        </button>
      )}
    </>
  )
}
