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

// Per-exercise named equipment ("Trap bar" = 25kg, "Leg press sled" = 75kg
// empty) — saved deliberately (not just remembered from last use), so it
// shows as its own chip only on the exercise it was set for, never bleeding
// into other exercises' bar lists. Kept in kg (the app's canonical weight
// unit). localStorage rather than the backend since this is a device-local
// convenience, not workout data.
const CUSTOM_EQUIPMENT_PREFIX = 'callahan.plateCalc.customEquipment.'

export function getCustomEquipment(exerciseId) {
  try {
    const raw = localStorage.getItem(CUSTOM_EQUIPMENT_PREFIX + exerciseId)
    if (raw === null) return null
    const parsed = JSON.parse(raw)
    const kg = Number(parsed?.kg)
    if (Number.isNaN(kg) || kg <= 0) return null
    return { name: typeof parsed.name === 'string' ? parsed.name : '', kg }
  } catch {
    return null
  }
}

export function setCustomEquipment(exerciseId, { name, kg }) {
  try {
    localStorage.setItem(CUSTOM_EQUIPMENT_PREFIX + exerciseId, JSON.stringify({ name: name || '', kg }))
  } catch {
    // Best-effort — private browsing / storage-full just means it won't save.
  }
}

export function clearCustomEquipment(exerciseId) {
  try {
    localStorage.removeItem(CUSTOM_EQUIPMENT_PREFIX + exerciseId)
  } catch {
    // Best-effort.
  }
}

// Per-exercise opt-out — cable/machine exercises with a pin-selected stack
// have no plates to load, so the calculator trigger is just noise there.
const HIDDEN_PREFIX = 'callahan.plateCalc.hidden.'

export function isCalculatorHiddenFor(exerciseId) {
  try {
    return localStorage.getItem(HIDDEN_PREFIX + exerciseId) === '1'
  } catch {
    return false
  }
}

export function setCalculatorHiddenFor(exerciseId, hidden) {
  try {
    if (hidden) localStorage.setItem(HIDDEN_PREFIX + exerciseId, '1')
    else localStorage.removeItem(HIDDEN_PREFIX + exerciseId)
  } catch {
    // Best-effort.
  }
}
