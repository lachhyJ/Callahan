// Rest-timer alert audio, built around two iOS Safari constraints:
//
//  A. The hardware silent switch mutes the Web Audio API (unlike <audio>/<video>
//     media elements). Playing a looping <audio> element first sets the page's
//     iOS audio session to the "playback" category; Web Audio output created /
//     resumed after that rides the same session and is no longer muted by the
//     switch. That looping element (near-silent) is the "keepalive".
//
//  B. A backgrounded tab's JS (setTimeout / rAF / React ticks) is suspended, so
//     we can't *trigger* the beep at T=0 from the main thread. Instead the beep
//     is pre-scheduled on the Web Audio audio-thread clock the moment the rest
//     starts (a real user gesture — completing a set), via
//     oscillator.start(ctx.currentTime + delay). That fires even while JS is
//     frozen, as long as the AudioContext stays alive — which the keepalive
//     element is there to ensure.
//
// All of this is iOS-fragile by nature (the OS can still cull the keepalive or
// suspend the context after long backgrounding). The reactive playBeepNow()
// path is kept as a foreground fallback.

let ctx = null
let keepaliveEl = null
let scheduledNodes = [] // oscillators for the pending beep
let scheduledFireAt = null // audio-clock time the pending scheduled beep sounds

function getAudioContextClass() {
  return window.AudioContext || window.webkitAudioContext
}

// Minimal valid mono 8 kHz 16-bit PCM WAV, ~0.4 s, amplitude ±1 LSB — audibly
// silent but not digital-black, so iOS won't treat the element as "nothing
// playing" and stop it. Built at runtime so no multi-KB base64 lives in source.
function silentWavUrl() {
  const sampleRate = 8000
  const samples = sampleRate * 0.4
  const dataBytes = samples * 2
  const buffer = new ArrayBuffer(44 + dataBytes)
  const view = new DataView(buffer)
  const writeStr = (offset, str) => {
    for (let i = 0; i < str.length; i++) view.setUint8(offset + i, str.charCodeAt(i))
  }
  writeStr(0, 'RIFF')
  view.setUint32(4, 36 + dataBytes, true)
  writeStr(8, 'WAVE')
  writeStr(12, 'fmt ')
  view.setUint32(16, 16, true) // PCM chunk size
  view.setUint16(20, 1, true) // format = PCM
  view.setUint16(22, 1, true) // channels
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * 2, true) // byte rate
  view.setUint16(32, 2, true) // block align
  view.setUint16(34, 16, true) // bits per sample
  writeStr(36, 'data')
  view.setUint32(40, dataBytes, true)
  for (let i = 0; i < samples; i++) view.setInt16(44 + i * 2, i % 2 ? 1 : -1, true)
  return URL.createObjectURL(new Blob([buffer], { type: 'audio/wav' }))
}

// Call from within a user gesture that begins a workout (opening a template,
// completing a set). Idempotent. Establishes the audio session + a running
// AudioContext so later scheduled beeps aren't muted by the silent switch and
// survive backgrounding.
export function unlockAudio() {
  const AudioContextClass = getAudioContextClass()
  if (!AudioContextClass) return

  if (!keepaliveEl) {
    keepaliveEl = new Audio(silentWavUrl())
    keepaliveEl.loop = true
    keepaliveEl.preload = 'auto'
    // No `muted` — a muted element does not promote the audio session.
    keepaliveEl.volume = 1
  }
  // Play (or resume) the keepalive first so the AudioContext below inherits the
  // "playback" session. A rejected promise (no gesture yet) is fine — the next
  // gesture retries.
  keepaliveEl.play().catch(() => {})

  if (!ctx) ctx = new AudioContextClass()
  if (ctx.state === 'suspended') ctx.resume().catch(() => {})
}

// Two stacked triangle tones, twice — enough harmonic content to cut through a
// music track. Shared by the scheduled and immediate paths.
function emitTones(startAt) {
  if (!ctx) return
  for (const delay of [0, 0.3]) {
    for (const frequency of [880, 1320]) {
      const oscillator = ctx.createOscillator()
      const gain = ctx.createGain()
      oscillator.type = 'triangle'
      oscillator.frequency.value = frequency
      const t = startAt + delay
      gain.gain.setValueAtTime(0.0001, t)
      gain.gain.exponentialRampToValueAtTime(0.45, t + 0.01)
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.25)
      oscillator.connect(gain)
      gain.connect(ctx.destination)
      oscillator.start(t)
      oscillator.stop(t + 0.25)
      scheduledNodes.push(oscillator)
    }
  }
}

// Pre-schedule the alert to sound `delaySeconds` from now on the audio-thread
// clock. Call from the gesture that starts/adjusts a rest. Replaces any
// previously scheduled beep.
export function scheduleBeep(delaySeconds) {
  unlockAudio()
  if (!ctx) return
  cancelScheduledBeep()
  if (ctx.state === 'suspended') ctx.resume().catch(() => {})
  const fireAt = ctx.currentTime + Math.max(0, delaySeconds)
  scheduledFireAt = fireAt
  emitTones(fireAt)
}

// Stop a pending scheduled beep (rest adjusted, skipped, next set started,
// workout finished, page unmounted).
export function cancelScheduledBeep() {
  for (const node of scheduledNodes) {
    try {
      node.stop()
    } catch {
      // already stopped / finished
    }
  }
  scheduledNodes = []
  scheduledFireAt = null
}

// Immediate beep — foreground fallback for the reactive countdown effect, and
// for platforms where scheduling drifts. No-op safe if audio was never unlocked.
// Suppressed if a scheduled beep for roughly this moment has already fired (or
// is about to), so a foregrounded countdown doesn't double up with it.
export function playBeepNow() {
  const AudioContextClass = getAudioContextClass()
  if (!AudioContextClass) return
  if (!ctx) ctx = new AudioContextClass()
  if (ctx.state === 'suspended') ctx.resume().catch(() => {})
  if (scheduledFireAt != null && ctx.currentTime >= scheduledFireAt - 1 && ctx.currentTime <= scheduledFireAt + 2) {
    return
  }
  emitTones(ctx.currentTime)
}
