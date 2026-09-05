import { Capacitor, registerPlugin } from '@capacitor/core'

// Rest-timer alert beep.
//
// Native (Capacitor) schedules it on the audio clock and it sounds backgrounded
// and through the silent switch — see the native path below. On the web it is
// foreground-only, for the reasons recorded here.
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
//
// The beep does briefly interrupt the user's music (`playback` is the only
// session that plays through the silent switch), and iOS won't auto-resume it
// because it treats us as a full media player, not a transient ducking prompt —
// and a web page can't send a system "play" command to resume it either. The
// one lever: release the <audio> element the instant the beep ends (drop src +
// load()), giving iOS the cleanest possible "interruption over", which on some
// iOS versions lets the previous Now Playing app resume on its own. Not
// guaranteed; the reliable fix is a native AVAudioSession with
// mixWithOthers/duckOthers.

// ── Native path ──
// The Capacitor wrap exists for exactly the two problems described above. A
// native AVAudioSession can be `.playback` (audible through the silent switch)
// AND `.mixWithOthers` + `.duckOthers` (music dips and comes back) at the same
// time, which no web audio session can. And with UIBackgroundModes: audio plus
// a player armed on the audio clock, the beep sounds while the app is
// backgrounded — the case the web version could never cover, because iOS
// suspends the webview's JS.
//
// Everything below the native branch is the web/PWA implementation, unchanged.

const RestAudio = registerPlugin('RestAudio')
const isNative = Capacitor.isNativePlatform()

/// Whether the native beep path is in play — the countdown effect uses this to
/// avoid sounding a second beep on top of the scheduled one.
export const isNativeAudio = isNative

// Arm the beep for a rest that ends at `endAt` (ms epoch). Native only — on the
// web the countdown effect plays it on the tick instead, and a backgrounded
// phone relies on the push notification.
export function scheduleBeep(endAt, { title, body } = {}) {
  if (!isNative) return
  RestAudio.schedule({ endAt, title, body }).catch(() => {})
}

// Temporary instrumentation for the rest-timer ducking (added 2026-09-05).
// Returns the native side's event diary so a backgrounded rest can be read back
// after the fact — see RestAudioPlugin's Diary section for why. Remove with it.
export async function restAudioDiagnostics({ clear = false } = {}) {
  if (!isNative) return null
  try {
    return await RestAudio.diagnostics({ clear })
  } catch {
    return null
  }
}

export function cancelScheduledBeep() {
  if (!isNative) return
  RestAudio.cancel().catch(() => {})
}

const BEEP_SRC = '/beep.m4a'

let beepEl = null
let primed = false

// Assigning src always starts a load, so only set it when it's actually clear
// (releaseEl clears it after every play).
function ensureSrc() {
  if (!beepEl.getAttribute('src')) beepEl.src = BEEP_SRC
}

// Fully release the media element / audio session. Safe to call repeatedly.
function releaseEl() {
  if (!beepEl) return
  try {
    beepEl.pause()
    beepEl.removeAttribute('src')
    beepEl.load()
  } catch {
    /* ignore */
  }
}

// Call from a user gesture (opening a workout, completing a set). Creates the
// element and primes it with a volume-0 play so the countdown-tick effect can
// call play() later without a user gesture of its own.
export function unlockAudio() {
  if (isNative) {
    // No gesture unlock needed natively; just make sure the session category is
    // set before anything tries to play.
    RestAudio.prepare().catch(() => {})
    return
  }
  if (!beepEl) {
    beepEl = new Audio()
    beepEl.preload = 'auto'
    beepEl.addEventListener('ended', releaseEl)
  }
  if (primed) return
  primed = true
  ensureSrc()
  const v = beepEl.volume
  beepEl.volume = 0
  beepEl.play().then(() => {
    beepEl.pause()
    beepEl.volume = v
    releaseEl()
  }).catch(() => { beepEl.volume = v; primed = false })
}

// Play the beep now. Called from the rest-countdown effect when it hits zero
// (which only advances while the tab is foregrounded) and from the workout
// screen's test button. releaseEl runs on 'ended'.
export function playBeepNow() {
  if (isNative) {
    RestAudio.beepNow().catch(() => {})
    return
  }
  unlockAudio()
  if (!beepEl) return
  try {
    ensureSrc()
    beepEl.volume = 1
    beepEl.currentTime = 0
    beepEl.play().catch(() => { /* blocked outside a gesture before first unlock */ })
  } catch {
    /* ignore */
  }
}
