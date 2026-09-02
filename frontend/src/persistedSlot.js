// Both the active workout and the rest timer need the same thing: one JSON
// value in localStorage, plus a way for any mounted component to hear about
// changes to it. They were two byte-identical modules differing only in the
// storage key and the event name.
//
// The change event is what makes the two bottom-tab bars, the global rest bar
// and the active workout page agree without prop-drilling or a context —
// localStorage on its own fires no event in the tab that wrote it.
export function createPersistedSlot(key, changeEvent) {
  function save(state) {
    localStorage.setItem(key, JSON.stringify(state))
    window.dispatchEvent(new Event(changeEvent))
  }

  function load() {
    const raw = localStorage.getItem(key)
    if (!raw) return null
    try {
      return JSON.parse(raw)
    } catch {
      return null
    }
  }

  function clear() {
    localStorage.removeItem(key)
    window.dispatchEvent(new Event(changeEvent))
  }

  function onChange(callback) {
    window.addEventListener(changeEvent, callback)
    return () => window.removeEventListener(changeEvent, callback)
  }

  return { save, load, clear, onChange }
}
