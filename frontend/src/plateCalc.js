// Shared by the standalone plate calculator page and the inline popover on
// the active workout page — keeps the plate-math and per-exercise bar-weight
// memory in one place instead of duplicated across both.

export const PLATE_SETS = {
  kg: [25, 20, 15, 10, 5, 2.5, 1.25],
  lb: [45, 35, 25, 10, 5, 2.5],
}

export const BAR_PRESETS = {
  kg: [
    { label: 'Olympic (20kg)', value: 20 },
    { label: "Women's Olympic (15kg)", value: 15 },
    { label: 'Training bar (10kg)', value: 10 },
  ],
  lb: [
    { label: 'Olympic (45lb)', value: 45 },
    { label: "Women's Olympic (35lb)", value: 35 },
    { label: 'Training bar (25lb)', value: 25 },
  ],
}

// Greedy fill: takes as many of the biggest plate as fit, then the next
// size down, etc. Only wrong when the target can't be hit exactly with the
// available denominations, which the leftover-weight readout covers.
export function calculatePlates(perSideWeight, availablePlates) {
  let remaining = Math.round(perSideWeight * 100) / 100
  const breakdown = []
  for (const plate of availablePlates) {
    const count = Math.floor(remaining / plate + 1e-9)
    if (count > 0) {
      breakdown.push({ plate, count })
      remaining = Math.round((remaining - count * plate) * 100) / 100
    }
  }
  return { breakdown, remainder: Math.max(remaining, 0) }
}

// Per-exercise "what bar/sled do I load this on" memory, keyed by exerciseId
// and kept in kg (the app's canonical weight unit) regardless of what unit
// the picker was last shown in. localStorage rather than the backend since
// this is a device-local convenience, not workout data.
const BAR_STORAGE_PREFIX = 'callahan.plateCalc.barKg.'

export function getRememberedBarKg(exerciseId) {
  try {
    const raw = localStorage.getItem(BAR_STORAGE_PREFIX + exerciseId)
    if (raw === null) return null
    const value = Number(raw)
    return Number.isNaN(value) ? null : value
  } catch {
    return null
  }
}

export function setRememberedBarKg(exerciseId, barKg) {
  try {
    localStorage.setItem(BAR_STORAGE_PREFIX + exerciseId, String(barKg))
  } catch {
    // Best-effort — private browsing / storage-full just means it won't remember.
  }
}
