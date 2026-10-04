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

// The zones worth offering by hand: Australia, the US, and a short list of
// places likely to come up. Any IANA id still works through the API/automatic
// prompt - this only keeps the picker short.
export const ZONE_GROUPS = [
  {
    label: 'Australia',
    zones: [
      ['Australia/Melbourne', 'Melbourne / Sydney / Canberra / Hobart'],
      ['Australia/Brisbane', 'Brisbane (no daylight saving)'],
      ['Australia/Adelaide', 'Adelaide'],
      ['Australia/Darwin', 'Darwin'],
      ['Australia/Perth', 'Perth'],
    ],
  },
  {
    label: 'United States',
    zones: [
      ['America/Los_Angeles', 'Pacific - Seattle / Los Angeles'],
      ['America/Denver', 'Mountain - Denver'],
      ['America/Phoenix', 'Arizona - Phoenix'],
      ['America/Chicago', 'Central - Chicago'],
      ['America/New_York', 'Eastern - New York'],
      ['America/Anchorage', 'Alaska - Anchorage'],
      ['Pacific/Honolulu', 'Hawaii - Honolulu'],
    ],
  },
  {
    label: 'Elsewhere',
    zones: [
      ['America/Vancouver', 'Vancouver'],
      ['Pacific/Auckland', 'Auckland'],
      ['Asia/Singapore', 'Singapore'],
      ['Asia/Tokyo', 'Tokyo'],
      ['Asia/Hong_Kong', 'Hong Kong'],
      ['Asia/Dubai', 'Dubai'],
      ['Europe/London', 'London'],
      ['Europe/Paris', 'Paris / Berlin / Rome'],
      ['UTC', 'UTC'],
    ],
  },
]

const KNOWN_ZONES = new Set(ZONE_GROUPS.flatMap((g) => g.zones.map(([id]) => id)))

export function isListedZone(zone) {
  return KNOWN_ZONES.has(zone)
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
