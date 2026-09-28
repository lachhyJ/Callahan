import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const api = vi.hoisted(() => ({
  getPlateCalcSettings: vi.fn(() => Promise.resolve({})),
  putPlateCalcSetting: vi.fn(() => Promise.resolve()),
  deletePlateCalcSetting: vi.fn(() => Promise.resolve()),
}))
vi.mock('./api/client', () => api)

import {
  DUMBBELL_STEPS_KG, PLATE_SETS, calculatePlates, getAvailablePlates,
  getCustomEquipment, getEquipmentType, guessEquipmentType, hydratePlateCalcSettings,
  nearestDumbbells, clearCustomEquipment,
  setAvailablePlates, setCustomEquipment, setEquipmentTypeOverride, describePlateDelta, calculatePlateDelta, plateLoadFor } from './plateCalc'

// The storage-backed helpers are the ones with real branching (fallbacks,
// validation, order preservation), so they need a localStorage.
import { installStorageStub } from './test/storageStub'

describe('calculatePlates', () => {
  const kg = PLATE_SETS.kg

  it('loads nothing for an empty bar', () => {
    expect(calculatePlates(0, kg)).toEqual({ breakdown: [], remainder: 0 })
  })

  it('fills greedily, biggest plate first', () => {
    expect(calculatePlates(45, kg).breakdown).toEqual([{ plate: 25, count: 1 }, { plate: 20, count: 1 }])
  })

  it('stacks multiples of the same plate', () => {
    expect(calculatePlates(62.5, kg).breakdown).toEqual([
      { plate: 25, count: 2 }, { plate: 10, count: 1 }, { plate: 2.5, count: 1 },
    ])
  })

  it('reports what it could not make', () => {
    // 1kg is below the smallest plate, so it is all remainder.
    expect(calculatePlates(1, kg)).toEqual({ breakdown: [], remainder: 1 })
  })

  it('clears the target exactly when the plates allow it', () => {
    const { breakdown, remainder } = calculatePlates(2.5 * 3, kg)
    expect(breakdown).toEqual([{ plate: 5, count: 1 }, { plate: 2.5, count: 1 }])
    expect(remainder).toBe(0)
  })

  // Deliberately not covered: the `+ 1e-9` epsilon in the greedy loop. A
  // brute-force sweep of every 0.01 step from 0 to 300 in both units found
  // zero inputs where removing it changes the result — every plate size is
  // exactly representable in binary and `remaining` is re-rounded to 2dp each
  // pass, so the quotient never lands just below an integer. It's cheap
  // insurance against a future non-representable plate (1.1kg, say), not
  // live behaviour, and a test asserting it would be asserting nothing.

  it('never reports a negative remainder', () => {
    expect(calculatePlates(0.0001, kg).remainder).toBeGreaterThanOrEqual(0)
  })
})

describe('nearestDumbbells', () => {
  it('returns an exact match alone', () => {
    expect(nearestDumbbells(20, DUMBBELL_STEPS_KG)).toEqual({ exact: 20 })
  })

  it('brackets a weight the rack does not have', () => {
    expect(nearestDumbbells(11, DUMBBELL_STEPS_KG)).toEqual({ below: 10, above: 12.5 })
  })

  it('omits the missing side at the bottom of the rack', () => {
    expect(nearestDumbbells(0.5, DUMBBELL_STEPS_KG)).toEqual({ below: undefined, above: 1 })
  })

  it('omits the missing side at the top of the rack', () => {
    expect(nearestDumbbells(60, DUMBBELL_STEPS_KG)).toEqual({ below: 50, above: undefined })
  })

  it('sorts an unordered rack before bracketing', () => {
    expect(nearestDumbbells(11, [20, 5, 12.5, 10])).toEqual({ below: 10, above: 12.5 })
  })
})

describe('guessEquipmentType', () => {
  it.each([
    ['DB Lateral Raise', 'dumbbell'],
    ['Incline DB Press', 'dumbbell'],
    ['Bulgarian Split Squat (DB)', 'dumbbell'],
    ['Dumbbell Curl', 'dumbbell'],
    ['Cable Row', 'hidden'],
    ['Pull-up', 'hidden'],
    ['Plank', 'hidden'],
    ['Barbell Squat', 'barbell'],
    ['Deadlift', 'barbell'],
  ])('%s -> %s', (name, expected) => {
    expect(guessEquipmentType(name)).toBe(expected)
  })

  it('falls through to barbell for names carrying no equipment hint', () => {
    // Documented behaviour: showing an unneeded button beats hiding a needed
    // one, since the override is one tap away.
    expect(guessEquipmentType('Lunges')).toBe('barbell')
    expect(guessEquipmentType('Single Leg Hamstring Curl')).toBe('barbell')
  })

  it('is case-insensitive', () => {
    expect(guessEquipmentType('db lateral raise')).toBe('dumbbell')
    expect(guessEquipmentType('CABLE ROW')).toBe('hidden')
  })

  it('handles a missing name', () => {
    expect(guessEquipmentType('')).toBe('barbell')
    expect(guessEquipmentType(undefined)).toBe('barbell')
  })
})

describe('storage-backed settings', () => {
  beforeEach(() => installStorageStub())
  afterEach(() => { delete globalThis.localStorage })

  it('defaults to the full plate set', () => {
    expect(getAvailablePlates('kg')).toEqual(PLATE_SETS.kg)
  })

  it('restores descending order however the toggles were saved', () => {
    // calculatePlates' greedy fill is only correct on a descending list.
    setAvailablePlates('kg', [2.5, 25, 10])
    expect(getAvailablePlates('kg')).toEqual([25, 10, 2.5])
  })

  it('drops saved sizes that PLATE_SETS no longer lists', () => {
    setAvailablePlates('kg', [25, 999])
    expect(getAvailablePlates('kg')).toEqual([25])
  })

  it('falls back to the full set rather than returning nothing', () => {
    setAvailablePlates('kg', [])
    expect(getAvailablePlates('kg')).toEqual(PLATE_SETS.kg)
  })

  it('survives malformed stored JSON', () => {
    localStorage.setItem('callahan.plateCalc.availablePlates.kg', 'not json')
    expect(getAvailablePlates('kg')).toEqual(PLATE_SETS.kg)
  })

  it('rejects custom equipment with a non-positive or unparseable weight', () => {
    setCustomEquipment(1, { name: 'Trap bar', kg: 25 })
    expect(getCustomEquipment(1)).toEqual({ name: 'Trap bar', kg: 25 })

    setCustomEquipment(2, { name: 'Broken', kg: 0 })
    expect(getCustomEquipment(2)).toBeNull()

    setCustomEquipment(3, { name: 'Broken', kg: 'heavy' })
    expect(getCustomEquipment(3)).toBeNull()
  })

  it('lets an override beat the name guess', () => {
    expect(getEquipmentType(9, 'Cable Row')).toBe('hidden')
    setEquipmentTypeOverride(9, 'barbell')
    expect(getEquipmentType(9, 'Cable Row')).toBe('barbell')
  })

  it('ignores an override value that is not a known type', () => {
    localStorage.setItem('callahan.plateCalc.equipmentType.9', 'nonsense')
    expect(getEquipmentType(9, 'Cable Row')).toBe('hidden')
  })

  it('accepts added weight as an override, though it is never guessed', () => {
    // Pull-ups are unweighted more often than not, so the guess stays 'hidden'
    // and the dip-belt mode is reached deliberately, per exercise.
    expect(guessEquipmentType('Weighted Pull-Ups')).toBe('hidden')
    setEquipmentTypeOverride(10, 'added')
    expect(getEquipmentType(10, 'Weighted Pull-Ups')).toBe('added')
  })
})

describe('server sync', () => {
  beforeEach(() => {
    installStorageStub()
    api.getPlateCalcSettings.mockClear()
    api.putPlateCalcSetting.mockClear()
    api.deletePlateCalcSetting.mockClear()
    api.getPlateCalcSettings.mockResolvedValue({})
  })
  afterEach(() => { delete globalThis.localStorage })

  it('pushes each setter to the server under the prefix-stripped key', () => {
    setAvailablePlates('kg', [25, 20])
    expect(api.putPlateCalcSetting).toHaveBeenCalledWith('availablePlates.kg', [25, 20])

    setCustomEquipment(11, { name: 'Trap bar', kg: 25 })
    expect(api.putPlateCalcSetting).toHaveBeenCalledWith('customEquipment.11', { name: 'Trap bar', kg: 25 })

    setEquipmentTypeOverride(11, 'barbell')
    expect(api.putPlateCalcSetting).toHaveBeenCalledWith('equipmentType.11', 'barbell')
  })

  it('pushes a delete when custom equipment is cleared', () => {
    clearCustomEquipment(11)
    expect(api.deletePlateCalcSetting).toHaveBeenCalledWith('customEquipment.11')
  })

  // The failure path is an async rejection, which a not.toThrow() around the
  // synchronous call could never see. So the server call returns a rejected
  // promise that records whether anyone attached a rejection handler to it.
  it('handles a failed server write rather than leaving the rejection unhandled', async () => {
    const failure = Promise.reject(new Error('offline'))
    failure.catch(() => {}) // keep the test runner itself quiet
    let handled = false
    api.putPlateCalcSetting.mockReturnValueOnce({
      then: (onOk, onErr) => { if (onErr) handled = true; return failure.then(onOk, onErr) },
      catch: (onErr) => { handled = true; return failure.catch(onErr) },
    })
    setAvailablePlates('kg', [25])
    expect(handled).toBe(true)
  })

  it('hydrate writes JSON values and raw strings into localStorage the way the getters read them', async () => {
    api.getPlateCalcSettings.mockResolvedValue({
      'availablePlates.kg': [25, 20, 10],
      'customEquipment.11': { name: 'Trap bar', kg: 25 },
      'equipmentType.11': 'added',
    })

    await hydratePlateCalcSettings()

    expect(getAvailablePlates('kg')).toEqual([25, 20, 10])
    expect(getCustomEquipment(11)).toEqual({ name: 'Trap bar', kg: 25 })
    expect(localStorage.getItem('callahan.plateCalc.equipmentType.11')).toBe('added')
    expect(getEquipmentType(11, 'Trap Bar Deadlift')).toBe('added')
  })

  it('hydrate leaves localStorage untouched when the fetch fails', async () => {
    setCustomEquipment(11, { name: 'Local only', kg: 22.5 })
    api.putPlateCalcSetting.mockClear()
    api.getPlateCalcSettings.mockRejectedValue(new Error('offline'))

    await hydratePlateCalcSettings()

    expect(getCustomEquipment(11)).toEqual({ name: 'Local only', kg: 22.5 })
  })
})

describe('plateLoadFor', () => {
  it('halves a barbell load after taking the bar off', () => {
    expect(plateLoadFor('barbell', 100, 20)).toBe(40)
  })

  // 20kg hung from a belt is one 20 - not the 10-per-side a bar would give.
  it('loads added weight as one stack, with no halving', () => {
    expect(plateLoadFor('added', 20, 0)).toBe(20)
  })

  it('takes a plate-loaded machine frame off first', () => {
    expect(plateLoadFor('added', 100, 30)).toBe(70)
  })
})

describe('describePlateDelta', () => {
  it('lists removals before additions, per side for a barbell', () => {
    expect(describePlateDelta({ toRemove: [{ plate: 15, count: 1 }], toAdd: [{ plate: 25, count: 1 }] }, true))
      .toBe('Remove 1×15kg per side · Add 1×25kg per side')
  })

  it('omits the side suffix and empty halves', () => {
    expect(describePlateDelta({ toRemove: [], toAdd: [{ plate: 2.5, count: 2 }, { plate: 1.25, count: 1 }] }, false))
      .toBe('Add 2×2.5kg, 1×1.25kg')
  })
})

// Per side, in kg, against the standard plate set.
describe('calculatePlateDelta', () => {
  const kg = PLATE_SETS.kg

  it('adds only the difference when the new weight builds on the old one', () => {
    expect(calculatePlateDelta(45, 47.5, kg)).toEqual({ toAdd: [{ plate: 2.5, count: 1 }], toRemove: [] })
  })

  // The canonical breakdown swaps the 15 for a 25 rather than adding a 10 on
  // top: the known naive answer the 2026-09-25 decision chose not to optimise.
  it('swaps plates when the canonical breakdowns differ', () => {
    expect(calculatePlateDelta(15, 25, kg)).toEqual({ toAdd: [{ plate: 25, count: 1 }], toRemove: [{ plate: 15, count: 1 }] })
  })

  it('is empty when nothing changes', () => {
    expect(calculatePlateDelta(60, 60, kg)).toEqual({ toAdd: [], toRemove: [] })
  })

  it('strips everything going back to an empty bar', () => {
    expect(calculatePlateDelta(25, 0, kg)).toEqual({ toAdd: [], toRemove: [{ plate: 25, count: 1 }] })
  })
})
