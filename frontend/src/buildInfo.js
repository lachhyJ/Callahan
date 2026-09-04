// `__BUILD_INFO__` is injected by vite.config.js at build time — see there for
// what it captures and why. `null` when git wasn't available to read from.
export const buildInfo = typeof __BUILD_INFO__ === 'undefined' ? null : __BUILD_INFO__

// "restaudio@6d6dcc9+", or "Callahan · main@a87f7c0" when the worktree name
// doesn't already say which branch it's for.
export function buildInfoLabel() {
  if (!buildInfo) return null
  const { worktree, branch, commit, dirty } = buildInfo
  const ref = `${branch}@${commit}${dirty ? '+' : ''}`
  return worktree === 'Callahan' ? ref : `${worktree} · ${ref}`
}
