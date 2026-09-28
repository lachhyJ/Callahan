import { Capacitor, registerPlugin } from '@capacitor/core'

// The webview always runs whatever's deployed (capacitor.config.json's
// server.url), so it can't answer "which git branch/commit was the native
// shell actually built from" — that's baked in at Xcode build time and read
// back through this plugin. Same idea also carries the free-provisioning
// profile's real expiry, read at runtime — see AppInfoPlugin.swift.

const AppInfo = registerPlugin('AppInfo')
const isNative = Capacitor.isNativePlatform()

// { branch, commit, dirty, provisioningExpiresAt } or null on the
// web / on any failure. provisioningExpiresAt is an ISO string or null (no
// embedded profile — always true in the Simulator, which isn't code-signed
// with one at all).
// Read once per app load: it's fixed at build time and the Dashboard footer
// and the expiry banner both ask for it.
let statusPromise = null

export function getNativeStatus() {
  if (!isNative) return Promise.resolve(null)
  statusPromise ??= AppInfo.getStatus().catch(() => null)
  return statusPromise
}

// Whole days until an ISO instant, rounded up (so "expires later today" is 1).
export function daysUntil(isoDate, now = Date.now()) {
  return Math.ceil((new Date(isoDate).getTime() - now) / (24 * 60 * 60 * 1000))
}

export function nativeBuildTag({ branch, commit, dirty, provisioningExpiresAt }) {
  const base = `native · ${branch}@${commit}${dirty ? '+' : ''}`
  if (!provisioningExpiresAt) return base

  const days = daysUntil(provisioningExpiresAt)
  const signingText = days <= 0 ? 'signing expired' : days === 1 ? 'signing expires in 1 day' : `signing expires in ${days}d`
  return `${base} · ${signingText}`
}
