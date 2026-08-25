// Shared by the standalone plate calculator page and the inline popover on
// the active workout page — keeps the plate-math and per-exercise bar-weight
// memory in one place instead of duplicated across both.

export const PLATE_SETS = {
  kg: [25, 20, 15, 10, 5, 2.5, 1.25],
  lb: [45, 35, 25, 10, 5, 2.5],
}

export const BAR_PRESETS = {
  kg: [
    { label: '20kg', value: 20 },
    { label: '15kg', value: 15 },
  ],
  lb: [
    { label: '45lb', value: 45 },
    { label: '35lb', value: 35 },
    { label: '25lb', value: 25 },
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

// Which plate sizes the athlete's gym actually has, per unit — device-wide
// rather than per-exercise, since it's a property of the gym, not the
// movement. Defaults to the full standard set until pared down.
const AVAILABLE_PLATES_PREFIX = 'callahan.plateCalc.availablePlates.'

export function getAvailablePlates(unit) {
  try {
    const raw = localStorage.getItem(AVAILABLE_PLATES_PREFIX + unit)
    if (raw === null) return PLATE_SETS[unit]
    const kept = JSON.parse(raw)
    // Preserves PLATE_SETS' descending order (required by calculatePlates'
    // greedy fill) regardless of the order toggles were saved in, and drops
    // any stale sizes a future PLATE_SETS edit might remove.
    const filtered = PLATE_SETS[unit].filter((p) => kept.includes(p))
    return filtered.length > 0 ? filtered : PLATE_SETS[unit]
  } catch {
    return PLATE_SETS[unit]
  }
}

export function setAvailablePlates(unit, plates) {
  try {
    localStorage.setItem(AVAILABLE_PLATES_PREFIX + unit, JSON.stringify(plates))
  } catch {
    // Best-effort.
  }
}

// Fixed dumbbell increments the athlete's gym actually racks — device-wide,
// same reasoning as available plates. Standard commercial-gym spacing.
export const DUMBBELL_STEPS_KG = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12.5, 15, 17.5, 20, 22.5, 25, 27.5, 30, 32.5, 35, 40, 45, 50]

const AVAILABLE_DUMBBELLS_PREFIX = 'callahan.plateCalc.availableDumbbells.'

export function getAvailableDumbbells() {
  try {
    const raw = localStorage.getItem(AVAILABLE_DUMBBELLS_PREFIX + 'kg')
    if (raw === null) return DUMBBELL_STEPS_KG
    const kept = JSON.parse(raw)
    const filtered = DUMBBELL_STEPS_KG.filter((d) => kept.includes(d))
    return filtered.length > 0 ? filtered : DUMBBELL_STEPS_KG
  } catch {
    return DUMBBELL_STEPS_KG
  }
}

export function setAvailableDumbbells(dumbbells) {
  try {
    localStorage.setItem(AVAILABLE_DUMBBELLS_PREFIX + 'kg', JSON.stringify(dumbbells))
  } catch {
    // Best-effort.
  }
}

// Given a target per-dumbbell weight, finds the closest available size(s).
// Returns an exact match alone when the rack has one, otherwise the
// nearest step below and above (either can be absent at the ends of the
// rack) so the athlete sees both directions to round rather than just
// whichever happens to be closer.
export function nearestDumbbells(perDumbbellKg, available) {
  const sorted = [...available].sort((a, b) => a - b)
  const exact = sorted.find((d) => Math.abs(d - perDumbbellKg) < 1e-9)
  if (exact !== undefined) return { exact }

  const below = [...sorted].reverse().find((d) => d < perDumbbellKg)
  const above = sorted.find((d) => d > perDumbbellKg)
  return { below, above }
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

// Name-based guess at what kind of equipment an exercise is loaded on —
// 'barbell' (bar/sled + plates), 'dumbbell' (fixed-weight, logged as
// combined weight across both hands), or 'hidden' (cable stacks, machines,
// bodyweight — nothing to load or calculate). Not perfect (a DB/Cable
// naming convention only helps for exercises tagged that way, and moves
// like "Lunges" or "Single Leg Hamstring Curl" carry no equipment hint at
// all) — those fall through to 'barbell' rather than 'hidden', since a
// manual override is one tap away either direction and showing an unneeded
// button is cheaper than hiding a needed one.
const HIDDEN_KEYWORDS = [
  'cable', 'machine',
  'push-up', 'pull-up', 'chin-up',
  'plank', 'dead bug', 'box jump', 'ab wheel', 'pallof', 'burpee', 'wall sit',
]

export function guessEquipmentType(exerciseName) {
  if (!exerciseName) return 'barbell'
  const name = exerciseName.toLowerCase()
  if (name.startsWith('db ') || name.includes(' db ') || name.includes('(db)') || name.includes('dumbbell')) {
    return 'dumbbell'
  }
  if (HIDDEN_KEYWORDS.some((keyword) => name.includes(keyword))) return 'hidden'
  return 'barbell'
}

// Per-exercise override on top of the name guess above — 'barbell' /
// 'dumbbell' / 'hidden' when the athlete has corrected it for a specific
// exercise, absent when left on the automatic guess.
const EQUIPMENT_TYPE_PREFIX = 'callahan.plateCalc.equipmentType.'

export function getEquipmentTypeOverride(exerciseId) {
  try {
    const value = localStorage.getItem(EQUIPMENT_TYPE_PREFIX + exerciseId)
    return value === 'barbell' || value === 'dumbbell' || value === 'hidden' ? value : null
  } catch {
    return null
  }
}

export function getEquipmentType(exerciseId, exerciseName) {
  return getEquipmentTypeOverride(exerciseId) ?? guessEquipmentType(exerciseName)
}

export function setEquipmentTypeOverride(exerciseId, type) {
  try {
    localStorage.setItem(EQUIPMENT_TYPE_PREFIX + exerciseId, type)
  } catch {
    // Best-effort.
  }
}

export function clearEquipmentTypeOverride(exerciseId) {
  try {
    localStorage.removeItem(EQUIPMENT_TYPE_PREFIX + exerciseId)
  } catch {
    // Best-effort.
  }
}
