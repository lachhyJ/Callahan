import { useEffect, useRef, useState } from 'react'
import { syncGarmin } from '../api/client'
import { SyncIcon, CheckIcon } from '../icons'

// The trigger returns its Python log tail; pull a one-line result out of it
// rather than showing raw log lines in the UI.
function summarise(result) {
  const log = Array.isArray(result?.log) ? result.log : []
  const done = [...log].reverse().find((l) => /Done: \d+ synced/.test(l) || /No Garmin activities/.test(l))
  const m = done && done.match(/Done: (\d+) synced/)
  if (m) {
    const n = Number(m[1])
    return n === 0 ? 'Up to date — nothing new.' : `Synced ${n} activit${n === 1 ? 'y' : 'ies'}.`
  }
  if (done) return 'Up to date — nothing new.'
  return result?.wellness ? 'Garmin + wellness synced.' : 'Garmin synced.'
}

// Shared by the Dashboard and the Games page. `variant="icon"` is a bare
// icon button (Dashboard header) that reports its result via onResult so the
// host places the message; the default `labelled` variant shows its own
// transient result line (Games header). onSynced refetches the host's data.
// Kept non-interactive for a beat after a sync finishes so a frustrated
// double/triple-tap can't fire a second (pointless, though harmless) run.
// Matches how long the result line stays up, so the button re-enables just
// as the "Synced …" confirmation fades rather than sitting dead afterwards.
const COOLDOWN_MS = 6000

export default function SyncGarminButton({ variant = 'labelled', onSynced, onResult, className }) {
  const [syncing, setSyncing] = useState(false)
  const [cooling, setCooling] = useState(false)
  const [msg, setMsg] = useState(null) // { text, isError } — labelled variant only
  const timer = useRef(null)
  const coolTimer = useRef(null)

  useEffect(() => () => {
    clearTimeout(timer.current)
    clearTimeout(coolTimer.current)
  }, [])

  function flash(payload) {
    setMsg(payload)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => setMsg(null), 6000)
  }

  async function run() {
    if (syncing || cooling) return
    setSyncing(true)
    setMsg(null)
    try {
      const result = await syncGarmin()
      const payload = { text: summarise(result), isError: false }
      if (onResult) onResult(payload)
      else flash(payload)
      onSynced?.()
    } catch (err) {
      const payload = { text: err.message, isError: true }
      if (onResult) onResult(payload)
      else flash(payload)
    } finally {
      setSyncing(false)
      setCooling(true)
      clearTimeout(coolTimer.current)
      coolTimer.current = setTimeout(() => setCooling(false), COOLDOWN_MS)
    }
  }

  const disabled = syncing || cooling

  const icon = <SyncIcon className={syncing ? 'icon-spin' : undefined} />

  if (variant === 'icon') {
    return (
      <button
        type="button"
        className={className ? `icon-link ${className}` : 'icon-link'}
        onClick={run}
        disabled={disabled}
        title="Sync Garmin"
        aria-label={syncing ? 'Syncing Garmin…' : 'Sync Garmin'}
      >
        {icon}
      </button>
    )
  }

  return (
    <div className={className ? `sync-garmin ${className}` : 'sync-garmin'}>
      <button type="button" className="secondary-btn sync-garmin-btn" onClick={run} disabled={disabled}>
        {icon}
        {syncing ? 'Syncing…' : 'Sync Garmin'}
      </button>
      {msg && (
        <p className={msg.isError ? 'sync-garmin-msg error' : 'sync-garmin-msg'}>
          {!msg.isError && <CheckIcon />}
          {msg.text}
        </p>
      )}
    </div>
  )
}
