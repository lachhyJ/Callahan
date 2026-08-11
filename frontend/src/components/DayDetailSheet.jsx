import { Link } from 'react-router-dom'

function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

export default function DayDetailSheet({ date, entry, onClose }) {
  const open = date !== null

  return (
    <>
      <div className={open ? 'sheet-backdrop visible' : 'sheet-backdrop'} onClick={onClose} />
      <div className={open ? 'day-detail-sheet open' : 'day-detail-sheet'} role="dialog" aria-label="Day detail">
        {date && (
          <>
            <div className="day-detail-sheet-header">
              <strong>{new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}</strong>
              <button type="button" className="sheet-close-btn" onClick={onClose} aria-label="Close">×</button>
            </div>
            {entry?.workouts.map((w) => (
              <Link key={`w-${w.id}`} to={`/sessions/${w.id}`} className="session-link" onClick={onClose}>
                {w.templateName ?? 'Workout'} · {w.setCount} set{w.setCount === 1 ? '' : 's'}
              </Link>
            ))}
            {entry?.runs.map((r) => (
              <p key={`r-${r.id}`}>
                Run · {r.distanceKm} km in {formatDuration(r.durationSeconds)}
              </p>
            ))}
          </>
        )}
      </div>
    </>
  )
}
