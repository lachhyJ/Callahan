const KEY = 'callahan_active_workout'
const CHANGE_EVENT = 'callahan-active-workout-changed'

export function saveActiveWorkout(state) {
  localStorage.setItem(KEY, JSON.stringify(state))
  window.dispatchEvent(new Event(CHANGE_EVENT))
}

export function loadActiveWorkout() {
  const raw = localStorage.getItem(KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw)
  } catch {
    return null
  }
}

export function clearActiveWorkout() {
  localStorage.removeItem(KEY)
  window.dispatchEvent(new Event(CHANGE_EVENT))
}

export function onActiveWorkoutChange(callback) {
  window.addEventListener(CHANGE_EVENT, callback)
  return () => window.removeEventListener(CHANGE_EVENT, callback)
}
