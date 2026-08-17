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

// Name-based guess at whether an exercise is loaded with plates on a bar or
// sled at all — cable stacks are pin-selected, dumbbells aren't loaded the
// same way, and most bodyweight moves just don't take plates. Not perfect
// (a DB/Cable naming convention only helps for exercises tagged that way,
// and moves like "Lunges" or "Single Leg Hamstring Curl" carry no equipment
// hint at all) — errs toward showing the button when unsure, since a manual
// override is one tap away either direction.
const NO_PLATES_KEYWORDS = [
  'cable', 'machine', 'dumbbell',
  'push-up', 'pull-up', 'chin-up',
  'plank', 'dead bug', 'box jump', 'ab wheel', 'pallof', 'burpee', 'wall sit',
]

export function looksPlateLoaded(exerciseName) {
  if (!exerciseName) return true
  const name = exerciseName.toLowerCase()
  if (name.startsWith('db ') || name.includes(' db ') || name.includes('(db)')) return false
  return !NO_PLATES_KEYWORDS.some((keyword) => name.includes(keyword))
}

// Per-exercise override on top of the name guess above — 'shown'/'hidden'
// when the athlete has corrected it for a specific exercise, absent when
// left on the automatic guess.
const VISIBILITY_PREFIX = 'callahan.plateCalc.visibility.'

export function getVisibilityOverride(exerciseId) {
  try {
    const value = localStorage.getItem(VISIBILITY_PREFIX + exerciseId)
    return value === 'shown' || value === 'hidden' ? value : null
  } catch {
    return null
  }
}

export function isCalculatorVisibleFor(exerciseId, exerciseName) {
  const override = getVisibilityOverride(exerciseId)
  if (override) return override === 'shown'
  return looksPlateLoaded(exerciseName)
}

export function setVisibilityOverride(exerciseId, visible) {
  try {
    localStorage.setItem(VISIBILITY_PREFIX + exerciseId, visible ? 'shown' : 'hidden')
  } catch {
    // Best-effort.
  }
}

export function clearVisibilityOverride(exerciseId) {
  try {
    localStorage.removeItem(VISIBILITY_PREFIX + exerciseId)
  } catch {
    // Best-effort.
  }
}
