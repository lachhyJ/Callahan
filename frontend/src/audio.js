// Rest-timer alert audio for iOS Safari / installed PWA.
//
// v1 tried Web Audio oscillators scheduled on the audio-thread clock, with a
// silent looping <audio> element to promote the iOS "playback" audio session so
// the hardware silent switch wouldn't mute them. On-device that produced *no
// sound at all*, even foregrounded from a tap — the silent-media session
// promotion didn't take, and the switch mutes Web Audio outright.
//
// v2 drops Web Audio entirely and plays everything through HTMLAudioElements,
// which DO sound through the silent switch when playback was user-initiated
// (same reason web videos play on a muted iPhone):
//
//  - `playBeepNow()` plays a short pre-generated beep WAV. Foreground / gesture
//    path (the countdown-tick fallback, and the test button).
//
//  - `scheduleBeep(delaySeconds)` generates a WAV that is `delaySeconds` of
//    silence followed by the beep, and plays it. The timing is baked into the
//    file, so it fires at the right moment even with the JS main thread frozen,
//    and media playback started before backgrounding keeps running. Called from
//    the set-completion / rest-adjust taps, so it's always user-initiated.
//
// Still iOS-fragile: whether a backgrounded standalone PWA keeps a media
// element playing to completion is not guaranteed. `lastStatus` records what
// the most recent play attempt did, for the on-screen test readout.

const SAMPLE_RATE = 8000

let beepEl = null // short beep, for playBeepNow()
let scheduledEl = null // silence + beep, for scheduleBeep()
let scheduledUrl = null // object URL currently held by scheduledEl
let beepUrl = null
let lastStatus = 'idle'

function setStatus(s) {
  lastStatus = s
}

// For the on-screen test readout — what the most recent play attempt did.
export function audioStatus() {
  return lastStatus
}

// Build a mono 16-bit PCM WAV: `leadSilenceSec` of near-silence, then a
// two-pulse 880/1320 Hz beep (~0.65 s). Returns an object URL.
function makeAlertWavUrl(leadSilenceSec = 0) {
  const lead = Math.max(0, Math.round(leadSilenceSec * SAMPLE_RATE))
  const pulse = Math.round(0.25 * SAMPLE_RATE)
  const gap = Math.round(0.15 * SAMPLE_RATE)
  const toneLen = pulse + gap + pulse
  const total = lead + toneLen
  const dataBytes = total * 2
  const buf = new ArrayBuffer(44 + dataBytes)
  const view = new DataView(buf)
  const put = (off, str) => { for (let i = 0; i < str.length; i++) view.setUint8(off + i, str.charCodeAt(i)) }
  put(0, 'RIFF'); view.setUint32(4, 36 + dataBytes, true); put(8, 'WAVE')
  put(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true)
  view.setUint16(22, 1, true); view.setUint32(24, SAMPLE_RATE, true)
  view.setUint32(28, SAMPLE_RATE * 2, true); view.setUint16(32, 2, true); view.setUint16(34, 16, true)
  put(36, 'data'); view.setUint32(40, dataBytes, true)

  const base = 44
  // Lead: alternating ±1 LSB, so it's not treated as digital-black / "nothing".
  for (let i = 0; i < lead; i++) view.setInt16(base + i * 2, i % 2 ? 1 : -1, true)

  const writePulse = (startSample) => {
    for (let i = 0; i < pulse; i++) {
      const t = i / SAMPLE_RATE
      // Short linear attack/release so it doesn't click.
      const env = Math.min(1, i / (0.01 * SAMPLE_RATE), (pulse - i) / (0.03 * SAMPLE_RATE))
      const s = env * 0.5 * (Math.sin(2 * Math.PI * 880 * t) * 0.6 + Math.sin(2 * Math.PI * 1320 * t) * 0.4)
      view.setInt16(base + (startSample + i) * 2, Math.max(-1, Math.min(1, s)) * 32767, true)
    }
  }
  writePulse(lead)
  for (let i = 0; i < gap; i++) view.setInt16(base + (lead + pulse + i) * 2, 0, true)
  writePulse(lead + pulse + gap)

  return URL.createObjectURL(new Blob([buf], { type: 'audio/wav' }))
}

// Call from a user gesture that begins a workout / completes a set. Creates the
// audio elements and primes beepEl with a muted play/pause so later .play()
// calls from non-gesture code (the countdown tick) are allowed.
export function unlockAudio() {
  if (!beepEl) {
    if (!beepUrl) beepUrl = makeAlertWavUrl(0)
    beepEl = new Audio(beepUrl)
    beepEl.preload = 'auto'
  }
  if (!scheduledEl) {
    scheduledEl = new Audio()
    scheduledEl.preload = 'auto'
  }
  beepEl.muted = true
  beepEl.play().then(() => {
    beepEl.pause()
    beepEl.currentTime = 0
    beepEl.muted = false
  }).catch(() => { beepEl.muted = false })
}

export function playBeepNow() {
  unlockAudio()
  if (!beepEl) return
  try {
    beepEl.currentTime = 0
    beepEl.play()
      .then(() => setStatus('playBeepNow: playing'))
      .catch((e) => setStatus('playBeepNow blocked: ' + e.name))
  } catch (e) {
    setStatus('playBeepNow threw: ' + e.name)
  }
}

// Play "delaySeconds of silence, then the beep" — timing baked into the file so
// it fires even if JS is suspended. Replaces any pending scheduled beep.
export function scheduleBeep(delaySeconds) {
  unlockAudio()
  if (!scheduledEl) return
  // Stop whatever's pending, but don't load() an empty src — assigning the new
  // src below is the load, and an interleaved load() aborts the play promise.
  try { scheduledEl.pause() } catch { /* ignore */ }
  if (scheduledUrl) URL.revokeObjectURL(scheduledUrl)

  scheduledUrl = makeAlertWavUrl(Math.max(0, delaySeconds))
  scheduledEl.src = scheduledUrl
  try {
    scheduledEl.currentTime = 0
    scheduledEl.play()
      .then(() => setStatus('scheduleBeep: playing (' + Math.round(delaySeconds) + 's lead)'))
      .catch((e) => setStatus('scheduleBeep blocked: ' + e.name))
  } catch (e) {
    setStatus('scheduleBeep threw: ' + e.name)
  }
}

export function cancelScheduledBeep() {
  if (scheduledEl) {
    try {
      scheduledEl.pause()
      scheduledEl.removeAttribute('src')
      scheduledEl.load()
    } catch { /* ignore */ }
  }
  if (scheduledUrl) {
    URL.revokeObjectURL(scheduledUrl)
    scheduledUrl = null
  }
}
