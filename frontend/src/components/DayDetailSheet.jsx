import SessionList from './SessionList'

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
            <SessionList workouts={entry?.workouts ?? []} runs={entry?.runs ?? []} onLinkClick={onClose} />
          </>
        )}
      </div>
    </>
  )
}
