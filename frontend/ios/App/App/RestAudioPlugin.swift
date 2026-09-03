import AVFoundation
import Capacitor
import Foundation

/// The rest-timer beep, natively.
///
/// This is the reason the Capacitor wrap exists. On the web there is no session
/// that is both audible through the hardware silent switch and polite to the
/// user's music: `playback` plays on silent but interrupts music and iOS will not
/// resume it, while the mixing/ducking sessions are themselves silenced by the
/// switch. AVAudioSession can do both at once — `.playback` for the silent
/// switch, `.mixWithOthers` + `.duckOthers` so music dips for the beep and comes
/// straight back instead of stopping.
///
/// Backgrounded audio needs more than a session: iOS suspends the app, and a
/// suspended app cannot start a sound. `UIBackgroundModes: audio` plus an
/// AVAudioPlayer armed with `play(atTime:)` covers it — the player counts down on
/// the audio hardware clock, which keeps the app alive to reach the beep and does
/// not depend on a timer that a suspended process would never fire.
@objc(RestAudioPlugin)
public class RestAudioPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "RestAudioPlugin"
    public let jsName = "RestAudio"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "prepare", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "schedule", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "cancel", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "beepNow", returnType: CAPPluginReturnPromise)
    ]

    private var player: AVAudioPlayer?
    private var sessionActive = false

    // MARK: - Session

    /// Ducking is requested per-activation, not baked into the category: an
    /// always-active ducking session would hold other apps quiet for the whole
    /// workout. We activate around the beep and stand the session down after.
    private func configureSession() throws {
        try AVAudioSession.sharedInstance().setCategory(
            .playback,
            mode: .default,
            options: [.mixWithOthers, .duckOthers]
        )
    }

    private func activate() {
        guard !sessionActive else { return }
        do {
            try configureSession()
            try AVAudioSession.sharedInstance().setActive(true)
            sessionActive = true
        } catch {
            CAPLog.print("RestAudio: could not activate session — \(error.localizedDescription)")
        }
    }

    /// `.notifyOthersOnDeactivation` is what tells whatever was playing that the
    /// interruption is over, so it un-ducks promptly rather than waiting for iOS
    /// to notice.
    private func deactivate() {
        guard sessionActive else { return }
        sessionActive = false
        try? AVAudioSession.sharedInstance().setActive(false, options: [.notifyOthersOnDeactivation])
    }

    private func loadPlayer() -> AVAudioPlayer? {
        guard let url = Bundle.main.url(forResource: "beep", withExtension: "m4a") else {
            CAPLog.print("RestAudio: beep.m4a missing from the app bundle")
            return nil
        }
        return try? AVAudioPlayer(contentsOf: url)
    }

    // MARK: - API

    @objc func prepare(_ call: CAPPluginCall) {
        do {
            try configureSession()
            call.resolve(["ready": true])
        } catch {
            call.resolve(["ready": false, "reason": error.localizedDescription])
        }
    }

    /// Arms the beep for `endAt`. Uses the audio clock rather than a Timer so it
    /// survives the app being backgrounded — a suspended process's timers do not
    /// fire, but an armed audio player keeps the app alive and sounds on time.
    @objc func schedule(_ call: CAPPluginCall) {
        guard let endAtMs = call.getDouble("endAt") else {
            call.reject("endAt is required")
            return
        }
        let seconds = Date(timeIntervalSince1970: endAtMs / 1000).timeIntervalSinceNow
        guard seconds > 0.25 else {
            // Too close to arm reliably — just sound it.
            playImmediately()
            call.resolve(["scheduled": false, "played": true])
            return
        }

        player?.stop()
        activate()
        guard let p = loadPlayer() else {
            call.resolve(["scheduled": false, "reason": "asset missing"])
            return
        }
        p.prepareToPlay()
        p.delegate = self
        player = p
        let ok = p.play(atTime: p.deviceCurrentTime + seconds)
        call.resolve(["scheduled": ok, "inSeconds": seconds])
    }

    @objc func cancel(_ call: CAPPluginCall) {
        player?.stop()
        player = nil
        deactivate()
        call.resolve()
    }

    @objc func beepNow(_ call: CAPPluginCall) {
        playImmediately()
        call.resolve()
    }

    private func playImmediately() {
        player?.stop()
        activate()
        guard let p = loadPlayer() else { return }
        p.delegate = self
        player = p
        p.play()
    }
}

extension RestAudioPlugin: AVAudioPlayerDelegate {
    /// Stand the session down as soon as the beep finishes so the user's music
    /// un-ducks immediately rather than staying quiet for the rest of the set.
    public func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        deactivate()
    }
}
