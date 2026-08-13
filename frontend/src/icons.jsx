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

export function DashboardIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <rect x="2" y="2" width="7" height="7" rx="1.5" />
      <rect x="11" y="2" width="7" height="7" rx="1.5" />
      <rect x="2" y="11" width="7" height="7" rx="1.5" />
      <rect x="11" y="11" width="7" height="7" rx="1.5" />
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

export function FlameIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M10 2c1 3-3 4-3 7.5a3 3 0 0 0 6 0c1 0 1.5 1 1.5 2a4.5 4.5 0 0 1-9 0C5.5 8 8 6.5 10 2z" />
    </svg>
  )
}

export function DocumentIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M6 2h6l3 3v12a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z" />
      <path d="M12 2v3h3M7 10h6M7 13h6M7 16h3" />
    </svg>
  )
}

export function TaperIcon(props) {
  return (
    <svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M3 4h14l-5 7v5l-4 2v-7z" />
    </svg>
  )
}
