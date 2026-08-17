const KEY = 'callahan_rest_timer'
const CHANGE_EVENT = 'callahan-rest-timer-changed'

export function saveRestTimer(state) {
  localStorage.setItem(KEY, JSON.stringify(state))
  window.dispatchEvent(new Event(CHANGE_EVENT))
}

export function loadRestTimer() {
  const raw = localStorage.getItem(KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw)
  } catch {
    return null
  }
}

export function clearRestTimer() {
  localStorage.removeItem(KEY)
  window.dispatchEvent(new Event(CHANGE_EVENT))
}

export function onRestTimerChange(callback) {
  window.addEventListener(CHANGE_EVENT, callback)
  return () => window.removeEventListener(CHANGE_EVENT, callback)
}
