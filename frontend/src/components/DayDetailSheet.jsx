import { useEffect, useRef } from 'react'
import SessionList from './SessionList'

export default function DayDetailSheet({ date, entry, onClose }) {
  const open = date !== null
  const sheetRef = useRef(null)
  const previousFocus = useRef(null)

  useEffect(() => {
    if (!open) return

    previousFocus.current = document.activeElement
    sheetRef.current?.querySelector('.sheet-close-btn')?.focus()

    function handleKeyDown(e) {
      if (e.key === 'Escape') {
        onClose()
        return
      }
      if (e.key !== 'Tab') return
      const focusable = sheetRef.current?.querySelectorAll('button, a[href], input, [tabindex]:not([tabindex="-1"])')
      if (!focusable || focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      previousFocus.current?.focus?.()
    }
  }, [open, onClose])

  return (
    <>
      <div className={open ? 'sheet-backdrop visible' : 'sheet-backdrop'} onClick={onClose} />
      <div ref={sheetRef} className={open ? 'day-detail-sheet open' : 'day-detail-sheet'} role="dialog" aria-modal="true" aria-label="Day detail">
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
