// One consistent icon family for the whole app: stroke-based, currentColor,
// uniform strokeWidth — replaces the ✓ / ▶ / 🔔 glyphs that stood in for
// real icons. Sizes vary per mount context (matched to adjacent type size),
// stroke technique stays identical across all three.

export function CheckIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M4 10.5l4 4 8-9" />
    </svg>
  )
}

export function PlayIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M6 4l9 6-9 6V4z" />
    </svg>
  )
}

export function BellIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M5 8a5 5 0 0 1 10 0c0 3.5 1.2 4.8 1.2 4.8H3.8S5 11.5 5 8z" />
      <path d="M8 15.5a2 2 0 0 0 4 0" />
    </svg>
  )
}
