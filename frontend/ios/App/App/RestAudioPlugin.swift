import AVFoundation
import Capacitor
import Foundation
import UserNotifications

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
    private var duckTimer: Timer?

    /// Quiet enough not to startle in a gym, loud enough over ducked music.
    private static let beepVolume: Float = 0.55
    /// How long before the beep to start ducking. Long enough for the dip to be
    /// under way when the sound lands, short enough not to be a silence.
    private static let duckLeadSeconds: TimeInterval = 0.35
    private static let notificationID = "callahan.rest.over"

    // MARK: - Session

    /// The session has to be *active* for the whole rest — that is what arms the
    /// audio clock and keeps the app alive to reach the beep while backgrounded.
    /// But `.duckOthers` on an active session ducks from the moment it is
    /// activated, which held the user's music down for the entire rest period.
    /// So ducking is not part of the resting category; it is switched on for a
    /// fraction of a second around the beep and switched straight back off.
    private func configureSession(ducking: Bool) throws {
        try AVAudioSession.sharedInstance().setCategory(
            .playback,
            mode: .default,
            options: ducking ? [.mixWithOthers, .duckOthers] : [.mixWithOthers]
        )
    }

    private func activate(ducking: Bool) {
        do {
            try configureSession(ducking: ducking)
            if !sessionActive {
                try AVAudioSession.sharedInstance().setActive(true)
                sessionActive = true
            }
        } catch {
            CAPLog.print("RestAudio: could not activate session — \(error.localizedDescription)")
        }
    }

    /// Opens the ducking window just before the armed beep. If this never fires
    /// — the app suspended despite the audio session — the beep still sounds, it
    /// just plays over the music instead of through a dip.
    private func scheduleDuck(inSeconds seconds: TimeInterval) {
        duckTimer?.invalidate()
        let lead = max(0, seconds - Self.duckLeadSeconds)
        duckTimer = Timer.scheduledTimer(withTimeInterval: lead, repeats: false) { [weak self] _ in
            self?.activate(ducking: true)
        }
    }

    /// `.notifyOthersOnDeactivation` is what tells whatever was playing that the
    /// interruption is over, so it un-ducks promptly rather than waiting for iOS
    /// to notice.
    private func deactivate() {
        duckTimer?.invalidate()
        duckTimer = nil
        guard sessionActive else { return }
        sessionActive = false
        try? AVAudioSession.sharedInstance().setActive(false, options: [.notifyOthersOnDeactivation])
    }

    /// Drop ducking but keep the session up, for the gap between one beep and
    /// the next rest — the music comes back without tearing down the session.
    private func stopDucking() {
        duckTimer?.invalidate()
        duckTimer = nil
        try? configureSession(ducking: false)
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
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { _, _ in }
        do {
            try configureSession(ducking: false)
            call.resolve(["ready": true])
        } catch {
            call.resolve(["ready": false, "reason": error.localizedDescription])
        }
    }

    /// A local notification fires on the device's own clock, so it lands on the
    /// second. The server push it replaces had to be scheduled, handed to APNs
    /// and delivered over the network, which is where the few seconds of lateness
    /// came from — and it needed a working connection besides.
    private func scheduleLocalNotification(inSeconds seconds: TimeInterval,
                                           title: String,
                                           body: String) {
        let centre = UNUserNotificationCenter.current()
        centre.removePendingNotificationRequests(withIdentifiers: [Self.notificationID])

        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        // The beep comes from the audio session, which is audible through the
        // silent switch; a notification sound would be silenced by it anyway.
        content.sound = nil
        content.interruptionLevel = .timeSensitive

        let request = UNNotificationRequest(
            identifier: Self.notificationID,
            content: content,
            trigger: UNTimeIntervalNotificationTrigger(timeInterval: seconds, repeats: false)
        )
        centre.add(request)
    }

    private func cancelLocalNotification() {
        UNUserNotificationCenter.current()
            .removePendingNotificationRequests(withIdentifiers: [Self.notificationID])
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
        activate(ducking: false)
        guard let p = loadPlayer() else {
            call.resolve(["scheduled": false, "reason": "asset missing"])
            return
        }
        p.volume = Self.beepVolume
        p.prepareToPlay()
        p.delegate = self
        player = p
        let ok = p.play(atTime: p.deviceCurrentTime + seconds)
        scheduleDuck(inSeconds: seconds)
        scheduleLocalNotification(
            inSeconds: seconds,
            title: call.getString("title") ?? "Rest over",
            body: call.getString("body") ?? "Next set."
        )
        call.resolve(["scheduled": ok, "inSeconds": seconds])
    }

    @objc func cancel(_ call: CAPPluginCall) {
        player?.stop()
        player = nil
        deactivate()
        cancelLocalNotification()
        call.resolve()
    }

    @objc func beepNow(_ call: CAPPluginCall) {
        playImmediately()
        call.resolve()
    }

    private func playImmediately() {
        player?.stop()
        activate(ducking: true)
        guard let p = loadPlayer() else { return }
        p.volume = Self.beepVolume
        p.delegate = self
        player = p
        p.play()
    }
}

extension RestAudioPlugin: AVAudioPlayerDelegate {
    /// Stand the session down as soon as the beep finishes so the user's music
    /// un-ducks immediately rather than staying quiet for the rest of the set.
    public func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        // Drop ducking immediately so music comes back, then stand the session
        // down — the rest is over, so nothing needs the audio clock any more.
        stopDucking()
        deactivate()
    }
}
