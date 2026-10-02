import { describe, expect, it } from 'vitest'
import { setPosition, setSlotFields } from './setSlot'

describe('setSlotFields', () => {
  it('formats a loaded set', () => {
    expect(setSlotFields({ exerciseName: 'Back Squat', targetReps: '5', targetWeightKg: 100, enteredReps: 5, nextSetNumber: 2, totalSets: 4 }))
      .toEqual({ exerciseName: 'Back Squat', targetReps: '5', targetWeight: '100 kg', enteredReps: '5', nextSetNumber: 2, totalSets: 4, warmupSets: 0 })
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

describe('setPosition', () => {
  it('counts warmups apart from working sets', () => {
    // 2 warmups + 4 working = 6 rows
    const at = (n) => setPosition({ nextSetNumber: n, totalSets: 6, warmupSets: 2 })
    expect([1, 2, 3, 4, 6].map(at)).toEqual(['W1/2', 'W2/2', '1/4', '2/4', '4/4'])
    expect(setPosition({ nextSetNumber: 3, totalSets: 6, warmupSets: 2 }, true)).toBe('set 1 of 4')
    expect(setPosition({ nextSetNumber: 1, totalSets: 6, warmupSets: 2 }, true)).toBe('warmup 1 of 2')
  })

  it('reads as plain set numbers with no warmups', () => {
    expect(setPosition({ nextSetNumber: 2, totalSets: 4 })).toBe('2/4')
  })

  it('is null past the last set', () => {
    expect(setPosition({ nextSetNumber: 7, totalSets: 6, warmupSets: 2 })).toBeNull()
    expect(setPosition({ nextSetNumber: 0, totalSets: 0 })).toBeNull()
  })
})
