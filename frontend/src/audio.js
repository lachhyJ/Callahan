// Rest-timer alert beep, foreground only.
//
// Backgrounded audio on iOS Safari / an installed PWA turned out to be a dead
// end for this use: the only audio session that plays through the hardware
// silent switch (`playback`) also interrupts the user's music and doesn't
// resume it, while the sessions that mix/duck other audio (`transient` /
// `ambient`) are themselves silenced by the switch. There is no web API for
// "audible on silent AND doesn't disturb music" — that needs a native
// AVAudioSession (playback + mixWithOthers + duckOthers), i.e. a Capacitor
// wrap. So the in-app beep is now foreground-only; a backgrounded / locked
// phone relies on the push notification instead.
//
// Failed web attempts, kept as a note so they aren't retried: Web Audio
// oscillators (muted by the silent switch); an <audio> blob: URL
// (NotSupportedError on iOS); an <audio> data:audio/wav URI of a generated PCM
// WAV (MEDIA_ERR_SRC_NOT_SUPPORTED — iOS <audio> wants AAC/MP3, not raw PCM);
// a long silence+beep asset played for the whole rest to bake in timing
// (holds a music-interrupting session the entire rest). What's left is a short
// static AAC file played on the countdown tick.

const BEEP_SRC = '/beep.m4a'

let beepEl = null
let primed = false

// Call from a user gesture (opening a workout, completing a set). Creates the
// element and primes it with a volume-0 play so the countdown-tick effect can
// call play() later without a user gesture of its own.
export function unlockAudio() {
  if (!beepEl) {
    beepEl = new Audio(BEEP_SRC)
    beepEl.preload = 'auto'
  }
  if (primed) return
  primed = true
  const v = beepEl.volume
  beepEl.volume = 0
  beepEl.play().then(() => {
    beepEl.pause()
    beepEl.currentTime = 0
    beepEl.volume = v
  }).catch(() => { beepEl.volume = v; primed = false })
}

// Play the beep now. Called from the rest-countdown effect when it hits zero
// (which only advances while the tab is foregrounded) and from the workout
// screen's test button.
export function playBeepNow() {
  unlockAudio()
  if (!beepEl) return
  try {
    beepEl.volume = 1
    beepEl.currentTime = 0
    beepEl.play().catch(() => { /* blocked outside a gesture before first unlock */ })
  } catch {
    /* ignore */
  }
}
