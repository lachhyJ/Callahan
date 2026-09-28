import { describe, expect, it } from 'vitest'
import { setSlotFields } from './setSlot'

describe('setSlotFields', () => {
  it('formats a loaded set', () => {
    expect(setSlotFields({ exerciseName: 'Back Squat', targetReps: '5', targetWeightKg: 100, enteredReps: 5, nextSetNumber: 2, totalSets: 4 }))
      .toEqual({ exerciseName: 'Back Squat', targetReps: '5', targetWeight: '100 kg', enteredReps: '5', nextSetNumber: 2, totalSets: 4 })
  })

  it('blanks the weight for bodyweight', () => {
    expect(setSlotFields({ exerciseName: 'Pull Up', targetReps: '8', targetWeightKg: 0, isBodyweight: true }).targetWeight).toBe('')
  })

  it('puts a hold in the weight slot and blanks both rep fields', () => {
    const f = setSlotFields({ exerciseName: 'Plank', targetReps: '1', enteredReps: 1, holdLabel: '30s/side' })
    expect(f.targetWeight).toBe('30s/side')
    expect(f.targetReps).toBe('')
    expect(f.enteredReps).toBe('')
  })

  // A null set number is a 400 from the schedule endpoint, so the defaults
  // matter on the watch path too, not only on the card.
  it('defaults the name and set numbers', () => {
    expect(setSlotFields({})).toMatchObject({ exerciseName: 'Workout', nextSetNumber: 1, totalSets: 1, targetReps: '', enteredReps: '' })
  })
})
