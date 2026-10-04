import { useEffect, useMemo, useState } from 'react'
import { getTimeZone, setTimeZone } from '../api/client'
import {
  TZ_CHANGED_EVENT, allZones, deviceZone, offsetLabel, writeManualZone, zoneLabel,
} from '../timeZone'

// Manual control over the zone the app computes "today" in, for when the
// automatic switch prompt is wrong or absent (no signal, VPN, a stopover). A
// zone picked here is remembered as a deliberate override so the prompt stops
// suggesting the phone's zone; "Use phone's zone" hands control back.
export default function TimeZoneSetting() {
  const [current, setCurrent] = useState(null)
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const device = deviceZone()
  const zones = useMemo(() => allZones(), [])

  useEffect(() => {
    const load = () => getTimeZone().then((tz) => setCurrent(tz.zone)).catch(() => {})
    load()
    window.addEventListener(TZ_CHANGED_EVENT, load)
    return () => window.removeEventListener(TZ_CHANGED_EVENT, load)
  }, [])

  async function apply(zone, manual) {
    setError(null)
    setSaving(true)
    try {
      const tz = await setTimeZone(zone)
      writeManualZone(manual ? tz.zone : null)
      setCurrent(tz.zone)
      window.dispatchEvent(new Event(TZ_CHANGED_EVENT))
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  if (!current) return null

  const options = zones.includes(current) ? zones : [current, ...zones]
  const followingPhone = device && current === device

  return (
    <div className="tz-setting section-gap">
      <label htmlFor="tz-select" className="tz-setting-label">
        Time zone <span className="tz-setting-offset">{offsetLabel(current)}</span>
      </label>
      <select
        id="tz-select"
        value={current}
        disabled={saving}
        onChange={(e) => apply(e.target.value, true)}
      >
        {options.map((z) => (
          <option key={z} value={z}>{zoneLabel(z)} ({z})</option>
        ))}
      </select>
      {device && !followingPhone && (
        <button type="button" className="secondary-btn" onClick={() => apply(device, false)} disabled={saving}>
          Use phone's zone ({zoneLabel(device)})
        </button>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
