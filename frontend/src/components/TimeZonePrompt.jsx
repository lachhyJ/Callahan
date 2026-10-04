import { useCallback, useEffect, useState } from 'react'
import { getTimeZone, setTimeZone } from '../api/client'

const DISMISS_KEY = 'callahan.tzPromptDismissed'

function deviceZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return null
  }
}

function readDismissed() {
  try {
    return sessionStorage.getItem(DISMISS_KEY)
  } catch {
    return null
  }
}

function zoneLabel(zone) {
  return zone.split('/').pop().replace(/_/g, ' ')
}

// Offers to move the server's "today" to the phone's zone when they differ
// (e.g. after flying). Re-checked whenever the app returns to the foreground.
// "Not now" is remembered for this session and device zone only, so it asks
// again after the next launch rather than being silenced for good.
export default function TimeZonePrompt() {
  const [serverZone, setServerZone] = useState(null)
  const [dismissed, setDismissed] = useState(readDismissed)
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const device = deviceZone()

  const refresh = useCallback(() => {
    getTimeZone().then((tz) => setServerZone(tz.zone)).catch(() => {})
  }, [])

  useEffect(() => {
    refresh()
    const onVisible = () => document.visibilityState === 'visible' && refresh()
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [refresh])

  async function switchZone() {
    setError(null)
    setSaving(true)
    try {
      const tz = await setTimeZone(device)
      setServerZone(tz.zone)
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  function dismiss() {
    try {
      sessionStorage.setItem(DISMISS_KEY, device)
    } catch {
      // Storage unavailable - dismiss for this mount only.
    }
    setDismissed(device)
  }

  if (!device || !serverZone || serverZone === device || dismissed === device) return null

  return (
    <div className="push-prompt" role="status">
      <span>Switch to {zoneLabel(device)} time? Your days are currently set to {zoneLabel(serverZone)}.</span>
      <button type="button" className="secondary-btn" onClick={switchZone} disabled={saving}>
        {saving ? 'Switching…' : 'Switch'}
      </button>
      <button type="button" className="secondary-btn" onClick={dismiss} disabled={saving}>Not now</button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
