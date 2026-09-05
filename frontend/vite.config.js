import { execFileSync } from 'node:child_process'
import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Stamps the bundle with where it was built from — which git worktree
// (Callahan vs. Callahan-<branch>), which branch, which commit, and whether
// the tree was clean. Surfaced in BuildFooter (bottom of the Dashboard; see
// buildInfo.js) so opening the app answers "which version is this" without
// going anywhere near Xcode or a terminal — the native shell has no
// equivalent of its own version string that updates per dev build, and
// CFBundleVersion in Info.plist is a static "1" that nothing bumps.
function readBuildInfo() {
  // The production build runs in Docker (see frontend/Dockerfile), whose
  // build context is just this frontend/ directory — no .git in there for
  // git to read, and node:22-alpine doesn't even have a git binary. deploy.sh
  // computes these on the NAS, where the real checkout lives one directory
  // up, and passes them through as build args/env instead. Local dev and
  // native (Xcode) builds don't set these, so they fall through to the git
  // shell-out below.
  if (process.env.CALLAHAN_GIT_COMMIT) {
    return {
      worktree: 'Callahan',
      branch: process.env.CALLAHAN_GIT_BRANCH || 'main',
      commit: process.env.CALLAHAN_GIT_COMMIT,
      dirty: false, // deploy.sh always deploys from a `git reset --hard`, so the tree is always clean
      builtAt: new Date().toISOString(),
    }
  }

  const git = (...args) => execFileSync('git', args, { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim()
  try {
    const root = git('rev-parse', '--show-toplevel')
    const dirty = git('status', '--porcelain').length > 0
    return {
      worktree: path.basename(root),
      branch: git('rev-parse', '--abbrev-ref', 'HEAD'),
      commit: git('rev-parse', '--short', 'HEAD'),
      dirty,
      builtAt: new Date().toISOString(),
    }
  } catch {
    // No git available (e.g. building from a tarball) — the label just won't
    // render rather than breaking the build over a diagnostic nicety.
    return null
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __BUILD_INFO__: JSON.stringify(readBuildInfo()),
  },
  server: {
    port: Number(process.env.PORT) || 5173,
  },
  test: {
    // Plain node — everything under test is pure logic. Component tests would
    // need jsdom; there aren't any yet, and the bugs worth catching here have
    // been in date maths and formatting rather than in rendering.
    environment: 'node',
    include: ['src/**/*.test.{js,jsx}'],
  },
})
