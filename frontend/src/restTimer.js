import { createPersistedSlot } from './persistedSlot'

// The running rest timer, shared between the active workout page (which owns
// the countdown while mounted) and the global rest bar (which takes over the
// moment you navigate away).
const slot = createPersistedSlot('callahan_rest_timer', 'callahan-rest-timer-changed')

export const saveRestTimer = slot.save
export const loadRestTimer = slot.load
export const clearRestTimer = slot.clear
export const onRestTimerChange = slot.onChange
