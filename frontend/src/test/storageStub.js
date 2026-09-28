// A Map-backed localStorage for the plain-node test environment (no jsdom).
// Returns the backing Map so a test can inspect or seed it directly.
export function installStorageStub() {
  const store = new Map()
  globalThis.localStorage = {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
    clear: () => store.clear(),
  }
  return store
}
