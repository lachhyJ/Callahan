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

export function BackIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M12 4l-6 6 6 6" />
    </svg>
  )
}

export function WorkoutIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M4 10h12" />
      <path d="M3 7v6M17 7v6" />
      <path d="M1 8.5v3M19 8.5v3" />
    </svg>
  )
}

export function RunIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M5 3v14" />
      <path d="M5 4h9l-2 3 2 3H5" />
    </svg>
  )
}

export function HistoryIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <circle cx="10" cy="10" r="7" />
      <path d="M10 6v4l3 2" />
    </svg>
  )
}

export function CalendarIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <rect x="3" y="4" width="14" height="13" rx="2" />
      <path d="M3 8h14M7 2v4M13 2v4" />
    </svg>
  )
}

export function ChartIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M5 16V9M10 16V4M15 16v-5" />
    </svg>
  )
}

export function ChevronRightIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M8 4l6 6-6 6" />
    </svg>
  )
}

export function ListIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M7 5h10M7 10h10M7 15h10" />
      <path d="M3 5h.01M3 10h.01M3 15h.01" />
    </svg>
  )
}

export function TrophyIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M6 3h8v5a4 4 0 0 1-8 0V3z" />
      <path d="M6 4H3.5A1.5 1.5 0 0 0 2 5.5C2 7.5 3.5 9 6 9M14 4h2.5A1.5 1.5 0 0 1 18 5.5C18 7.5 16.5 9 14 9" />
      <path d="M10 12v3M7 17h6M8 15h4v2H8z" />
    </svg>
  )
}
