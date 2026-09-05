import { Capacitor, registerPlugin } from '@capacitor/core'

// The webview always runs whatever's deployed (capacitor.config.json's
// server.url), so it can't answer "which git branch/commit was the native
// shell actually built from" — that's baked in at Xcode build time and read
// back through this plugin. Same idea also carries the free-provisioning
// profile's real expiry, read at runtime — see AppInfoPlugin.swift.

const AppInfo = registerPlugin('AppInfo')
const isNative = Capacitor.isNativePlatform()

// { branch, commit, dirty, builtAt, provisioningExpiresAt } or null on the
// web / on any failure. provisioningExpiresAt is an ISO string or null (no
// embedded profile — always true in the Simulator, which isn't code-signed
// with one at all).
export function getNativeStatus() {
  if (!isNative) return Promise.resolve(null)
  return AppInfo.getStatus().catch(() => null)
}

export function nativeBuildTag({ branch, commit, dirty }) {
  return `native · ${branch}@${commit}${dirty ? '+' : ''}`
}
