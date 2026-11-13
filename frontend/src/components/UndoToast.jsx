import { useEffect } from 'react'

// A discreet bottom snackbar with one Undo action. Dismisses itself.
export default function UndoToast({ message, onUndo, onDismiss, durationMs = 7000 }) {
  useEffect(() => {
    const t = setTimeout(onDismiss, durationMs)
    return () => clearTimeout(t)
  }, [message, onDismiss, durationMs])
  return (
    <div className="undo-toast" role="status">
      <span>{message}</span>
      <button type="button" onClick={onUndo}>Undo</button>
    </div>
  )
}
