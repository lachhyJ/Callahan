// Shared bits for the time zone prompt and the manual picker.
const MANUAL_KEY = 'callahan.tzManual'
export const TZ_CHANGED_EVENT = 'callahan-timezone-changed'

export function deviceZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone
  } catch {
    return null
  }
}

// The zone the user picked by hand. While the server is on it, the automatic
// "switch to the phone's zone?" prompt stays quiet - otherwise a deliberate
// override would be nagged about on every launch.
export function readManualZone() {
  try {
    return localStorage.getItem(MANUAL_KEY)
  } catch {
    return null
  }
}

export function writeManualZone(zone) {
  try {
    if (zone) localStorage.setItem(MANUAL_KEY, zone)
    else localStorage.removeItem(MANUAL_KEY)
  } catch {
    // Storage unavailable - the override just won't suppress the prompt.
  }
}

export function zoneLabel(zone) {
  return zone.split('/').pop().replace(/_/g, ' ')
}

export function allZones() {
  try {
    return Intl.supportedValuesOf('timeZone')
  } catch {
    return ['Australia/Melbourne', 'America/Los_Angeles', 'America/Vancouver', 'America/New_York', 'UTC']
  }
}

export function offsetLabel(zone, at = new Date()) {
  try {
    const part = new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'shortOffset' })
      .formatToParts(at).find((p) => p.type === 'timeZoneName')
    return part ? part.value.replace('GMT', 'UTC') : ''
  } catch {
    return ''
  }
}
