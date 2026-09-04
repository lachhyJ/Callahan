import { useEffect, useRef } from 'react'

// A confirm dialog that can tell you what kind of decision you are making.
//
// Both of the active workout's confirmations used to be `window.confirm`,
// which renders the same OS alert whichever one it is — so "you are about to
// throw away an hour of logged sets" and "you left three planned sets blank,
// save anyway?" looked identical at 1am with a barbell in front of you. The
// only difference was the sentence, and the sentence is the thing you skim.
//
// Two variants, deliberately not subtle:
//   • `danger`   — destructive and unrecoverable. Red rule, red confirm button,
//                  and the safe action is the one your thumb lands on first.
//   • `caution`  — recoverable and probably fine. Amber rule, ordinary accent
//                  confirm, and confirming is the expected outcome.
//
// A bottom sheet rather than a centred modal: this is a phone-first screen and
// the buttons need to be in thumb reach, which is also the app's existing
// language (see .day-detail-sheet / PlateCalcSheet).
export default function ConfirmSheet({
  open,
  variant = 'caution',
  title,
  body,
  detail,
  confirmLabel,
  cancelLabel = 'Cancel',
  onConfirm,
  onCancel,
}) {
  const confirmRef = useRef(null)
  const cancelRef = useRef(null)

  // Focus the SAFE action on open, whichever that is — for a destructive
  // sheet that is Cancel, so a stray Return keypress cannot discard a session.
  useEffect(() => {
    if (!open) return
    const safe = variant === 'danger' ? cancelRef.current : confirmRef.current
    safe?.focus()
  }, [open, variant])

  useEffect(() => {
    if (!open) return
    function onKey(e) {
      if (e.key === 'Escape') onCancel()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onCancel])

  return (
    <>
      <div
        className={open ? 'sheet-backdrop visible' : 'sheet-backdrop'}
        onClick={onCancel}
        aria-hidden="true"
      />
      <div
        className={`confirm-sheet confirm-sheet-${variant}${open ? ' open' : ''}`}
        role="alertdialog"
        aria-modal="true"
        aria-hidden={!open}
        aria-label={title}
      >
        <strong className="confirm-sheet-title">{title}</strong>
        <p className="confirm-sheet-body">{body}</p>
        {detail && <p className="confirm-sheet-detail">{detail}</p>}
        <div className="confirm-sheet-actions">
          <button type="button" ref={cancelRef} className="confirm-sheet-cancel" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button type="button" ref={confirmRef} className="confirm-sheet-confirm" onClick={onConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </>
  )
}
