// One consistent icon family for the whole app: stroke-based, 20x20,
// currentColor — replaces the ✓ / ▶ / 🔔 glyphs that stood in for real icons.

export function CheckIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M4 10.5l4 4 8-9" />
    </svg>
  )
}

export function PlayIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="12" height="12" fill="currentColor" {...props}>
      <path d="M5 3.5v13l11-6.5-11-6.5z" />
    </svg>
  )
}

export function BellIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M5 8a5 5 0 0 1 10 0c0 3.5 1.2 4.8 1.2 4.8H3.8S5 11.5 5 8z" />
      <path d="M8 15.5a2 2 0 0 0 4 0" />
    </svg>
  )
}
