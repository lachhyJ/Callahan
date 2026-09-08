import { useLayoutEffect, useRef } from 'react'

// The exercise focus cue. A plain <input> scrolled a long cue out of view one
// line at a time — "Minimum ground contact time, not height. Knees near…" with
// no way to read the rest. This is the same borderless italic field but grows
// to fit its text. Keeps the underline-on-focus affordance via .cue-input.
export default function CueInput({ value, onChange, onBlur, placeholder, ariaLabel }) {
  const ref = useRef(null)

  function fit(el) {
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${el.scrollHeight}px`
  }

  // Re-fit whenever the value changes from outside (initial load, a fetched
  // cue) as well as on every keystroke below.
  useLayoutEffect(() => {
    fit(ref.current)
  }, [value])

  return (
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
      onBlur={onBlur}
      aria-label={ariaLabel}
    />
  )
}
