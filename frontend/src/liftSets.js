// Rendering for a logged set, matching the backend's LiftBasis (see
// LiftProgress.cs). The set that actually happened is always shown; the
// estimate only appears when it's trustworthy for that exercise, and is
// always labelled as an estimate — an e1RM on its own is a number that was
// never lifted.

export const LIFT_BASIS = { e1rm: 'E1Rm', setVolume: 'SetVolume', assisted: 'Assisted' }

function kg(v) {
  const n = Number(v)
  return Number.isInteger(n) ? String(n) : n.toFixed(1)
}

// "240 kg × 12 (e1RM 336 kg)" · "40 kg × 18" · "−14 kg × 8" (assisted)
// · "× 9" (pure bodyweight, where the load carries no information)
export function formatSet(set, basis) {
  if (!set) return '—'
  const reps = `× ${set.reps}`

  if (basis === LIFT_BASIS.assisted) {
    if (Number(set.weightKg) === 0) return `bodyweight ${reps}`
    // Stored negative, but "assist" already carries the direction — printing
    // "-16 kg assist" doubles the negation and reads as a subtraction.
    // Positive weight on an assisted exercise is real added load.
    const w = Number(set.weightKg)
    return w < 0 ? `${kg(-w)} kg assist ${reps}` : `+${kg(w)} kg ${reps}`
  }

  const base = `${kg(set.weightKg)} kg ${reps}`
  return set.e1Rm != null ? `${base} (e1RM ${kg(set.e1Rm)} kg)` : base
}

// What the percentage is a percentage OF. Only shown when it isn't e1RM, so
// a +13% on accessory volume can't be mistaken for a +13% on a main lift.
export function basisNote(basis) {
  return basis === LIFT_BASIS.setVolume ? ' by set volume' : ''
}

// Assisted lifts have no meaningful percentage — the score is an ordering,
// not a magnitude — so they describe the change in load instead.
export function formatDelta(deltaPercent, from, to, basis) {
  if (deltaPercent != null) {
    return `${deltaPercent > 0 ? '+' : ''}${Number(deltaPercent).toFixed(1)}%${basisNote(basis)}`
  }
  if (basis !== LIFT_BASIS.assisted || !from || !to) return ''
  const assistOff = Number(from.weightKg) - Number(to.weightKg)
  if (assistOff < 0) return `${kg(-assistOff)} kg less assistance`
  if (assistOff > 0) return `${kg(assistOff)} kg more assistance`
  return `${to.reps - from.reps > 0 ? '+' : ''}${to.reps - from.reps} reps`
}
