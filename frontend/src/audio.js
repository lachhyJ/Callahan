// Rest-timer alert audio for iOS Safari / installed PWA.
//
// What did NOT work on-device, in order:
//  - v1: Web Audio oscillators. Silent — iOS mutes the Web Audio API with the
//    hardware silent switch, and the "play a silent media element to promote
//    the audio session" trick didn't hold.
//  - v2: HTMLAudioElement fed a blob: URL of a generated WAV. play() rejected
//    NotSupportedError — iOS won't take a blob-URL <audio> source.
//  - v3: HTMLAudioElement fed a data:audio/wav URI of a generated 8-bit WAV.
//    MEDIA_ERR_SRC_NOT_SUPPORTED (code 4) — iOS <audio> playback doesn't
//    reliably decode raw PCM WAV (Web Audio's decodeAudioData does; the media
//    element pipeline is pickier and wants AAC/MP3).
//
// v4: two static AAC assets, played through HTMLAudioElements. Media elements
// sound through the silent switch when playback is user-initiated (same reason
// web videos play on a muted iPhone), and iOS decodes AAC in <audio> fine.
//
//  - /beep.m4a          — the ~0.6 s double beep on its own. playBeepNow().
//  - /rest-alert.m4a    — REST_ALERT_LEAD_S seconds of silence, then the same
//    beep. scheduleBeep(delay) seeks to (lead - delay) and plays, so the beep
//    lands `delay` seconds later with the timing carried by the file — it fires
//    even if JS is suspended, and playback started before a backgrounding
//    keeps running (best-effort on iOS).
//
// v5: v4 played the beep but stopped the user's music and didn't resume it —
// the default <audio> session interrupts other audio. Setting
// navigator.audioSession.type = 'transient' makes the beep MIX with other
// audio (music ducks, then restores) while still ignoring the mute switch.
//
// audioStatus() reports the last play attempt for the on-screen tuning readout.

const BEEP_SRC = '/beep.m4a'
const REST_ALERT_SRC = '/rest-alert.m4a'
const REST_ALERT_LEAD_S = 300 // silence before the beep in rest-alert.m4a

let beepEl = null
let scheduledEl = null
let primed = false
let lastStatus = 'idle'

const setStatus = (s) => { lastStatus = s }
export function audioStatus() {
  return lastStatus
}

const describe = (e, el) => {
  const code = el && el.error ? ` mediaError=${el.error.code}` : ''
  return `${e && e.name ? e.name : String(e)}${code}`
}

// WebKit audio-session hint. 'transient' = a short sound that MIXES with other
// audio (the OS ducks e.g. music while it plays, then restores it) and is NOT
// silenced by the hardware mute switch — exactly what a rest-timer beep wants.
// Without this an <audio> element uses a 'playback' session that stops other
// audio and doesn't resume it. No-op where the API is absent.
function setTransientAudioSession() {
  try {
    if (navigator.audioSession) navigator.audioSession.type = 'transient'
  } catch { /* ignore */ }
}
setTransientAudioSession()

// Call from a user gesture that begins a workout / completes a set. Creates the
// elements and primes them with a volume-0 play so the countdown-tick fallback
// can call play() later outside a gesture.
export function unlockAudio() {
  setTransientAudioSession()
  if (!beepEl) {
    beepEl = new Audio(BEEP_SRC)
    beepEl.preload = 'auto'
  }
  if (!scheduledEl) {
    scheduledEl = new Audio(REST_ALERT_SRC)
    scheduledEl.preload = 'auto'
  }
  // Prime once, in this gesture, so the countdown-tick fallback can call play()
  // later without a user gesture. Re-priming on every call would race an
  // in-flight scheduleBeep() on the same element.
  if (primed) return
  primed = true
  for (const el of [beepEl, scheduledEl]) {
    const v = el.volume
    el.volume = 0
    el.play().then(() => {
      el.pause()
      el.currentTime = 0
      el.volume = v
    }).catch(() => { el.volume = v; primed = false })
  }
}

export function playBeepNow() {
  unlockAudio()
  if (!beepEl) return
  try {
    beepEl.volume = 1
    beepEl.currentTime = 0
    beepEl.play()
      .then(() => setStatus('playBeepNow: playing'))
      .catch((e) => setStatus('playBeepNow: ' + describe(e, beepEl)))
  } catch (e) {
    setStatus('playBeepNow threw: ' + describe(e, beepEl))
  }
}

// Seek into the silence+beep asset so the beep sounds `delaySeconds` from now.
export function scheduleBeep(delaySeconds) {
  unlockAudio()
  if (!scheduledEl) return
  const delay = Math.max(0, Math.min(delaySeconds, REST_ALERT_LEAD_S))
  try {
    scheduledEl.volume = 1
    scheduledEl.currentTime = REST_ALERT_LEAD_S - delay
    scheduledEl.play()
      .then(() => setStatus(`scheduleBeep: playing (${Math.round(delay)}s lead)`))
      .catch((e) => setStatus('scheduleBeep: ' + describe(e, scheduledEl)))
  } catch (e) {
    setStatus('scheduleBeep threw: ' + describe(e, scheduledEl))
  }
}

export function cancelScheduledBeep() {
  if (!scheduledEl) return
  try {
    scheduledEl.pause()
    scheduledEl.currentTime = 0
  } catch { /* ignore */ }
}
