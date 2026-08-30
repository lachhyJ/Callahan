// Rest-timer alert audio for iOS Safari / installed PWA.
//
// History of what did NOT work on-device:
//  - v1: Web Audio oscillators (scheduled on the audio-thread clock) + a silent
//    <audio> element to promote the iOS "playback" session. No sound at all,
//    even foregrounded — the hardware silent switch mutes Web Audio outright and
//    the session-promotion trick didn't hold.
//  - v2: HTMLAudioElement fed a `blob:` URL of a generated WAV. play() rejected
//    with NotSupportedError — iOS Safari won't decode a blob-URL <audio> source.
//
// v3: HTMLAudioElement fed a `data:audio/wav;base64,...` URI of an 8-bit PCM
// WAV generated at runtime. Media elements sound through the silent switch when
// playback is user-initiated (same reason web videos play on a muted iPhone).
//  - playBeepNow(): short beep, for the foreground countdown fallback + test.
//  - scheduleBeep(delay): `delay` seconds of silence then the beep, timing
//    baked into the file so it fires on time even with JS suspended and keeps
//    playing across a backgrounding. Always called from a tap.
//
// Still iOS-fragile (a backgrounded standalone PWA may not run a media element
// to completion). audioStatus() reports the last play attempt for on-screen
// tuning.

const SAMPLE_RATE = 8000

let beepEl = null
let scheduledEl = null
let lastStatus = 'idle'

const setStatus = (s) => { lastStatus = s }
export function audioStatus() {
  return lastStatus
}

function base64(bytes) {
  let bin = ''
  const chunk = 0x8000
  for (let i = 0; i < bytes.length; i += chunk) {
    bin += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk))
  }
  return btoa(bin)
}

// 8-bit unsigned mono PCM WAV: `leadSilenceSec` of silence (byte 128), then a
// two-pulse 880/1320 Hz beep (~0.65 s). Returns a data: URI. 8-bit keeps a
// 90 s clip near ~720 KB of base64 rather than ~1.9 MB at 16-bit.
function makeAlertDataUri(leadSilenceSec = 0) {
  const lead = Math.max(0, Math.round(leadSilenceSec * SAMPLE_RATE))
  const pulse = Math.round(0.25 * SAMPLE_RATE)
  const gap = Math.round(0.15 * SAMPLE_RATE)
  const total = lead + pulse + gap + pulse
  const buf = new ArrayBuffer(44 + total)
  const view = new DataView(buf)
  const bytes = new Uint8Array(buf)
  const put = (off, str) => { for (let i = 0; i < str.length; i++) view.setUint8(off + i, str.charCodeAt(i)) }
  put(0, 'RIFF'); view.setUint32(4, 36 + total, true); put(8, 'WAVE')
  put(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true) // PCM
  view.setUint16(22, 1, true) // mono
  view.setUint32(24, SAMPLE_RATE, true)
  view.setUint32(28, SAMPLE_RATE, true) // byte rate (8-bit mono)
  view.setUint16(32, 1, true) // block align
  view.setUint16(34, 8, true) // bits per sample
  put(36, 'data'); view.setUint32(40, total, true)

  const base = 44
  bytes.fill(128, base, base + lead) // silence = unsigned midpoint

  const writePulse = (startSample) => {
    for (let i = 0; i < pulse; i++) {
      const t = i / SAMPLE_RATE
      const env = Math.min(1, i / (0.01 * SAMPLE_RATE), (pulse - i) / (0.03 * SAMPLE_RATE))
      const s = env * 0.7 * (Math.sin(2 * Math.PI * 880 * t) * 0.6 + Math.sin(2 * Math.PI * 1320 * t) * 0.4)
      bytes[base + startSample + i] = Math.max(0, Math.min(255, Math.round(128 + s * 127)))
    }
  }
  writePulse(lead)
  bytes.fill(128, base + lead + pulse, base + lead + pulse + gap)
  writePulse(lead + pulse + gap)

  return 'data:audio/wav;base64,' + base64(bytes)
}

const errName = (e, el) => {
  const code = el && el.error ? ' mediaError=' + el.error.code : ''
  return (e && e.name ? e.name : String(e)) + code
}

// Call from a user gesture (opening a workout, completing a set). Creates the
// elements and primes beepEl with a volume-0 play so the countdown-tick
// fallback can later call play() outside a gesture.
export function unlockAudio() {
  if (!beepEl) {
    beepEl = new Audio(makeAlertDataUri(0))
    beepEl.preload = 'auto'
  }
  if (!scheduledEl) {
    scheduledEl = new Audio()
    scheduledEl.preload = 'auto'
  }
  const v = beepEl.volume
  beepEl.volume = 0
  beepEl.play().then(() => {
    beepEl.pause()
    beepEl.volume = v
  }).catch(() => { beepEl.volume = v })
}

export function playBeepNow() {
  unlockAudio()
  if (!beepEl) return
  try {
    beepEl.volume = 1
    beepEl.play()
      .then(() => setStatus('playBeepNow: playing'))
      .catch((e) => setStatus('playBeepNow: ' + errName(e, beepEl)))
  } catch (e) {
    setStatus('playBeepNow threw: ' + errName(e, beepEl))
  }
}

// Play "delaySeconds of silence, then the beep". Replaces any pending one.
export function scheduleBeep(delaySeconds) {
  unlockAudio()
  if (!scheduledEl) return
  try { scheduledEl.pause() } catch { /* ignore */ }
  scheduledEl.src = makeAlertDataUri(Math.max(0, delaySeconds))
  try {
    scheduledEl.play()
      .then(() => setStatus('scheduleBeep: playing (' + Math.round(delaySeconds) + 's lead)'))
      .catch((e) => setStatus('scheduleBeep: ' + errName(e, scheduledEl)))
  } catch (e) {
    setStatus('scheduleBeep threw: ' + errName(e, scheduledEl))
  }
}

export function cancelScheduledBeep() {
  if (!scheduledEl) return
  try {
    scheduledEl.pause()
    scheduledEl.removeAttribute('src')
    scheduledEl.load()
  } catch { /* ignore */ }
}
