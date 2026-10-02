#!/bin/sh
# Stamps the native build with which git branch/commit it was compiled from —
# the Xcode-side counterpart to vite.config.js's readBuildInfo(). The webview
# always loads the deployed site (capacitor.config.json's server.url), so
# buildInfo.js can only ever describe that deploy, never the local checkout a
# ⌘R was actually built from. This is what TopBar's native build tag reads.
#
# Run as an Xcode "Run Script" build phase, before Compile Sources, so
# BuildInfo.generated.swift exists before anything tries to compile it. The
# generated file is gitignored — this script is the only source of truth for
# it, and it's cheap enough to regenerate on every build.

set -e

REPO_ROOT=$(git -C "$SRCROOT" rev-parse --show-toplevel 2>/dev/null || true)
OUT="$SRCROOT/App/BuildInfo.generated.swift"

if [ -z "$REPO_ROOT" ]; then
  # No git available (e.g. an archive built from an exported source tarball) —
  # fall back to unknowns rather than failing the build over a diagnostic.
  BRANCH="unknown"
  COMMIT="unknown"
  DIRTY="false"
  BEHIND="0"
else
  BRANCH=$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)
  COMMIT=$(git -C "$REPO_ROOT" rev-parse --short HEAD)
  if [ -n "$(git -C "$REPO_ROOT" status --porcelain --untracked-files=no)" ]; then
    DIRTY="true"
  else
    DIRTY="false"
  fi
  # Commits on origin/main this checkout lacks, by the last-fetched ref (no
  # network in a build phase). A push from another worktree updates that ref,
  # so a stale primary checkout shows up here even without a fetch. 0 when
  # there is no origin/main to compare against.
  BEHIND=$(git -C "$REPO_ROOT" rev-list --count HEAD..origin/main 2>/dev/null || echo 0)
fi

cat > "$OUT" <<EOF
// Generated at build time by generate_build_info.sh — do not edit, do not commit.
enum NativeBuildInfo {
    static let branch = "$BRANCH"
    static let commit = "$COMMIT"
    static let dirty = $DIRTY
    static let behind = $BEHIND
}
EOF
