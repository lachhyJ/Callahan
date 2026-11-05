import { describe, expect, it } from 'vitest'
import { reconcileRest, restTimerFromNative } from './restReconcile'

const NOW = 1_000_000

function set(weightKg, reps, completed = false) {
  return { weightKg: String(weightKg), reps: String(reps), completed }
}

function session() {
  return [
    { exerciseName: 'Bench Press', restSeconds: 120, targetReps: '6', sets: [set(80, 6, true), set(80, 6, true), set(80, 6, false)] },
  ]
}

function localRest(overrides = {}) {
  return { exerciseName: 'Bench Press', endAt: NOW + 60_000, totalSeconds: 120, timerId: 'srv-1', nextSetNumber: 3, totalSets: 3, ...overrides }
}

describe('reconcileRest', () => {
  it('does nothing when neither side has a rest', () => {
    expect(reconcileRest(null, { active: false }, session(), NOW)).toEqual({ kind: 'none' })
  })

  it('clears the local rest when native says none is running (Skip on the card)', () => {
    expect(reconcileRest(localRest(), { active: false }, session(), NOW)).toEqual({ kind: 'clear', cancelTimerId: 'srv-1' })
  })

  it('clears without a cancel when the local rest never got a server timer', () => {
    expect(reconcileRest(localRest({ timerId: null }), { active: false }, session(), NOW)).toEqual({ kind: 'clear', cancelTimerId: null })
  })

  it('treats a native rest whose endAt has passed as inactive, not a rest to revive', () => {
    const native = { active: true, endAt: NOW - 1, serverTimerId: 'srv-9' }
    expect(reconcileRest(localRest(), native, session(), NOW).kind).toBe('clear')
    expect(reconcileRest(null, native, session(), NOW)).toEqual({ kind: 'none' })
  })

  it('treats endAt exactly at now as over', () => {
    expect(reconcileRest(null, { active: true, endAt: NOW, serverTimerId: 'x' }, session(), NOW)).toEqual({ kind: 'none' })
  })

  it('adopts a card-started rest, keeping the server timer the card already booked', () => {
    const native = { active: true, endAt: NOW + 90_000, totalSeconds: 120, serverTimerId: 'srv-card' }
    const r = reconcileRest(null, native, session(), NOW)
    expect(r.kind).toBe('set')
    expect(r.resyncEndAt).toBeNull()
    expect(r.rest).toMatchObject({ exerciseName: 'Bench Press', endAt: NOW + 90_000, totalSeconds: 120, timerId: 'srv-card', nextSetNumber: 3 })
  })

  it('adopts a card-started rest and asks for a server timer when the card could not book one', () => {
    const native = { active: true, endAt: NOW + 90_000, totalSeconds: 0 }
    const r = reconcileRest(null, native, session(), NOW)
    expect(r.resyncEndAt).toBe(NOW + 90_000)
    // totalSeconds falls back to the exercise's rest.
    expect(r.rest).toMatchObject({ timerId: null, totalSeconds: 120 })
  })

  it('adopts nothing when the workout has no set left to describe', () => {
    const done = [{ ...session()[0], sets: [set(80, 6, true)] }]
    expect(reconcileRest(null, { active: true, endAt: NOW + 5000 }, done, NOW)).toEqual({ kind: 'set', rest: null, resyncEndAt: null })
  })

  // The backlog's named case (2026-10-02): native holds a newer rest than JS remembers.
  it('moves to native\'s newer rest under the server timer the card booked', () => {
    const prev = localRest({ endAt: NOW + 5_000 })
    const native = { active: true, endAt: NOW + 115_000, serverTimerId: 'srv-2' }
    const r = reconcileRest(prev, native, session(), NOW)
    expect(r).toEqual({ kind: 'set', rest: { ...prev, endAt: NOW + 115_000, timerId: 'srv-2' }, resyncEndAt: null })
  })

  it('moves to native\'s endAt and reschedules the server timer when native has no newer one', () => {
    const prev = localRest()
    const native = { active: true, endAt: NOW + 75_000, serverTimerId: 'srv-1' }
    const r = reconcileRest(prev, native, session(), NOW)
    expect(r).toEqual({ kind: 'set', rest: { ...prev, endAt: NOW + 75_000, timerId: null }, resyncEndAt: NOW + 75_000 })
  })

  it('ignores drift of a second or less', () => {
    const prev = localRest()
    expect(reconcileRest(prev, { active: true, endAt: prev.endAt + 1000, serverTimerId: 'z' }, session(), NOW)).toEqual({ kind: 'none' })
    expect(reconcileRest(prev, { active: true, endAt: prev.endAt + 1001, serverTimerId: 'z' }, session(), NOW).kind).toBe('set')
  })
})

describe('restTimerFromNative', () => {
  it('describes the set the workout now points at', () => {
    expect(restTimerFromNative(session(), { endAt: 5, totalSeconds: 60 })).toMatchObject({ exerciseName: 'Bench Press', nextSetNumber: 3, endAt: 5, totalSeconds: 60, doneLabel: '' })
  })
})
