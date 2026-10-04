import { useCallback, useEffect, useState } from 'react'
import { getTimeZone, setTimeZone } from '../api/client'
import { TZ_CHANGED_EVENT, deviceZone as readDeviceZone, readManualZone, zoneLabel } from '../timeZone'

const DISMISS_KEY = 'callahan.tzPromptDismissed'

function readDismissed() {
  try {
    return sessionStorage.getItem(DISMISS_KEY)
  } catch {
    return null
  }
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
  const device = readDeviceZone()

  const refresh = useCallback(() => {
    getTimeZone().then((tz) => setServerZone(tz.zone)).catch(() => {})
  }, [])

  useEffect(() => {
    refresh()
    const onVisible = () => document.visibilityState === 'visible' && refresh()
    document.addEventListener('visibilitychange', onVisible)
    window.addEventListener(TZ_CHANGED_EVENT, refresh)
    return () => {
      document.removeEventListener('visibilitychange', onVisible)
      window.removeEventListener(TZ_CHANGED_EVENT, refresh)
    }
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

  if (!device || !serverZone || serverZone === device || dismissed === device
    || serverZone === readManualZone()) return null

  return (
    <div className="push-prompt tz-prompt" role="status">
      <span>Switch to {zoneLabel(device)} time? Your days are currently set to {zoneLabel(serverZone)}.</span>
      <button type="button" className="secondary-btn" onClick={switchZone} disabled={saving}>
        {saving ? 'Switching…' : 'Switch'}
      </button>
      <button type="button" className="secondary-btn" onClick={dismiss} disabled={saving}>Not now</button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
