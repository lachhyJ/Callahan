// `__BUILD_INFO__` is injected by vite.config.js at build time — see there for
// what it captures and why. `null` when git wasn't available to read from.
const buildInfo = typeof __BUILD_INFO__ === 'undefined' ? null : __BUILD_INFO__

// "a87f7c0" for main in the main checkout; otherwise "restaudio · rest@6d6dcc9+"
// style, so a worktree or branch build says what it is.
export function buildInfoLabel() {
  if (!buildInfo) return null
  const { worktree, branch, commit, dirty } = buildInfo
  const ref = `${branch === 'main' ? '' : `${branch}@`}${commit}${dirty ? '+' : ''}`
  return worktree === 'Callahan' ? ref : `${worktree} · ${ref}`
}
