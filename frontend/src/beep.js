// A single shared AudioContext, created inside the user gesture that starts
// a workout (see unlockAudioContext) and reused for every rest-timer beep
// after that. iOS creates a new AudioContext in a suspended state unless
// it's made inside a user gesture — so a fresh context per beep() call
// works the first time (created while handling the tap) but can go silent
// on every later, backgrounded fire. Reusing one context that was unlocked
// up front avoids that.
let sharedContext = null

function getAudioContextClass() {
  return window.AudioContext || window.webkitAudioContext
}

export function unlockAudioContext() {
  const AudioContextClass = getAudioContextClass()
  if (!AudioContextClass) return
  if (!sharedContext) sharedContext = new AudioContextClass()
  if (sharedContext.state === 'suspended') sharedContext.resume()
}

export function playBeep() {
  const AudioContextClass = getAudioContextClass()
  if (!AudioContextClass) return
  const ctx = sharedContext ?? (sharedContext = new AudioContextClass())
  for (const startDelay of [0, 0.3]) {
    // Two stacked frequencies (triangle, not pure sine) so the beep has more
    // harmonic content and cuts through a music track instead of blending
    // into it at the same perceived loudness.
    for (const frequency of [880, 1320]) {
      const oscillator = ctx.createOscillator()
      const gain = ctx.createGain()
      oscillator.type = 'triangle'
      oscillator.frequency.value = frequency
      const startTime = ctx.currentTime + startDelay
      gain.gain.setValueAtTime(0.0001, startTime)
      gain.gain.exponentialRampToValueAtTime(0.45, startTime + 0.01)
      gain.gain.exponentialRampToValueAtTime(0.0001, startTime + 0.25)
      oscillator.connect(gain)
      gain.connect(ctx.destination)
      oscillator.start(startTime)
      oscillator.stop(startTime + 0.25)
    }
  }
}
