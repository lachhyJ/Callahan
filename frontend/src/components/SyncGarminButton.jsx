import { useEffect, useRef, useState } from 'react'
import { syncGarmin } from '../api/client'

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

// Shared by the Dashboard and the Games page. Owns its own in-flight state
// and a transient result line; the host passes onSynced to refetch its data.
export default function SyncGarminButton({ onSynced, className }) {
  const [syncing, setSyncing] = useState(false)
  const [msg, setMsg] = useState(null) // { text, isError }
  const timer = useRef(null)

  useEffect(() => () => clearTimeout(timer.current), [])

  function flash(text, isError) {
    setMsg({ text, isError })
    clearTimeout(timer.current)
    timer.current = setTimeout(() => setMsg(null), 6000)
  }

  async function handleClick() {
    if (syncing) return
    setSyncing(true)
    setMsg(null)
    try {
      const result = await syncGarmin()
      flash(summarise(result), false)
      onSynced?.()
    } catch (err) {
      flash(err.message, true)
    } finally {
      setSyncing(false)
    }
  }

  return (
    <div className={className ? `sync-garmin ${className}` : 'sync-garmin'}>
      <button type="button" className="secondary-btn" onClick={handleClick} disabled={syncing}>
        {syncing ? 'Syncing…' : 'Sync Garmin'}
      </button>
      {msg && <p className={msg.isError ? 'sync-garmin-msg error' : 'sync-garmin-msg'}>{msg.text}</p>}
    </div>
  )
}
