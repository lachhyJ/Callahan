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
///
/// ## Why the audio clock needs a wall-clock guard
///
/// `play(atTime:)` counts on `deviceCurrentTime`, which only advances while the
/// audio hardware is running and whose zero point is whenever the hardware last
/// started. Re-arming mid-rest therefore reads a baseline that may have just
/// moved — `player?.stop()` can idle the hardware between the read and the arm —
/// and the beep lands early by however long the hardware had been up. Measured in
/// a real workout: beeps 3s and 8s early, and one rest that beeped twice.
///
/// Two defences, because the audio clock is the only clock that survives
/// suspension and so cannot simply be replaced with a wall-clock timer:
///   • `schedule` is idempotent — re-arming for an `endAt` we are already armed
///     for is a no-op, so ordinary UI churn cannot disturb a running countdown;
///   • the finish delegate checks the wall clock, and a beep that arrives more
///     than a second early is discarded and re-armed for the remainder rather
///     than being taken at face value.
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
    /// Backstop for the ducking window. Ducking is normally lifted by the player
    /// finishing, which means any path where the beep does not arrive — it was
    /// cancelled, the player failed to load, the session was torn down from
    /// under it — used to leave the user's music held down indefinitely. Seen in
    /// a real workout: ducking came on late and stayed on for the rest of the
    /// session. Nothing legitimately needs it for more than a beep's length.
    private var duckWatchdog: Timer?

    /// Wall-clock time the armed beep is meant to sound. The audio clock is what
    /// actually fires it; this is what we check that firing against.
    private var armedEndAt: Date?
    /// Kept so a re-arm can reissue the local notification unchanged.
    private var armedTitle = "Rest over"
    private var armedBody = "Next set."

    /// Quiet enough not to startle in a gym, loud enough over ducked music.
    private static let beepVolume: Float = 0.55
    /// How long before the beep to start ducking. Long enough for the dip to be
    /// under way when the sound lands, short enough not to be a silence.
    private static let duckLeadSeconds: TimeInterval = 0.35
    /// How early a beep may land before we treat it as the audio clock having
    /// slipped rather than as the rest genuinely being over.
    private static let earlyToleranceSeconds: TimeInterval = 1.0
    /// Longest ducking may ever stay on, measured from when it is switched on.
    /// Comfortably longer than the beep plus its lead-in, far shorter than a
    /// rest period — so a missed un-duck costs a second of quiet music, not a
    /// whole set of it.
    private static let duckMaxSeconds: TimeInterval = 3.0
    private static let notificationID = "callahan.rest.over"

    override public func load() {
        // The Live Activity's buttons mutate the timer in this same process while
        // the webview is suspended, so they cannot tell JS to re-arm the beep.
        // RestTimerStore posts this instead; without it a ±15s from the lock
        // screen moved the countdown and left the beep on the old schedule.
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleRestTimerChanged(_:)),
            name: .callahanRestTimerChanged,
            object: nil
        )
    }

    @objc private func handleRestTimerChanged(_ note: Notification) {
        let endAt = note.userInfo?[RestTimerChange.endAtKey] as? Date
        DispatchQueue.main.async {
            guard let endAt else {
                self.standDown()
                return
            }
            self.arm(for: endAt, force: true)
        }
    }

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
            if ducking { armDuckWatchdog() }
        } catch {
            CAPLog.print("RestAudio: could not activate session — \(error.localizedDescription)")
        }
    }

    /// Ducking is a momentary thing around a beep. If it is still on well after
    /// the beep should have come and gone, something did not run — lift it
    /// anyway rather than leaving the music down.
    private func armDuckWatchdog() {
        duckWatchdog?.invalidate()
        duckWatchdog = Timer.scheduledTimer(withTimeInterval: Self.duckMaxSeconds,
                                            repeats: false) { [weak self] _ in
            self?.stopDucking()
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
        duckWatchdog?.invalidate()
        duckWatchdog = nil
        guard sessionActive else { return }
        sessionActive = false
        try? AVAudioSession.sharedInstance().setActive(false, options: [.notifyOthersOnDeactivation])
    }

    /// Drop ducking but keep the session up, for the gap between one beep and
    /// the next rest — the music comes back without tearing down the session.
    private func stopDucking() {
        duckTimer?.invalidate()
        duckTimer = nil
        duckWatchdog?.invalidate()
        duckWatchdog = nil
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
    ///
    /// Idempotent unless `force`: the JS effect that calls this re-runs on state
    /// that has nothing to do with the timer, and a re-arm is not free — see the
    /// clock discussion on the type.
    @discardableResult
    private func arm(for endAt: Date, force: Bool) -> Bool {
        if !force, let armed = armedEndAt, player != nil,
           abs(armed.timeIntervalSince(endAt)) < Self.earlyToleranceSeconds {
            return true
        }

        guard endAt.timeIntervalSinceNow > 0.25 else {
            // Too close to arm reliably — just sound it.
            playImmediately()
            return false
        }

        // Ordering here is the whole fix for beeps landing early.
        //
        // `deviceCurrentTime` only advances while the audio hardware is running,
        // and its zero point is wherever the hardware last started. The previous
        // version stopped the outgoing player *first*, which can idle the
        // hardware, then activated the session and read the baseline — so the
        // baseline had moved between the stop and the read, and the beep landed
        // early by however long the hardware had been up. Measured at 3s and 8s
        // early in a real workout, and once as a double beep.
        //
        // So: bring the session and the new player up while the old one is still
        // holding the hardware open, read the baseline and the wall clock
        // together at the last possible moment, arm, and only then stop the
        // outgoing player. The hardware never idles across the re-arm, so the
        // clock the arm is expressed in cannot shift under it.
        let previous = player
        activate(ducking: false)
        guard let p = loadPlayer() else { return false }
        p.volume = Self.beepVolume
        p.delegate = self
        p.prepareToPlay()

        let baseline = p.deviceCurrentTime
        let seconds = endAt.timeIntervalSinceNow
        guard seconds > 0.05 else {
            previous?.stop()
            playImmediately()
            return false
        }

        player = p
        armedEndAt = endAt
        let ok = p.play(atTime: baseline + seconds)
        previous?.stop()
        scheduleDuck(inSeconds: seconds)
        scheduleLocalNotification(inSeconds: seconds, title: armedTitle, body: armedBody)
        return ok
    }

    @objc func schedule(_ call: CAPPluginCall) {
        guard let endAtMs = call.getDouble("endAt") else {
            call.reject("endAt is required")
            return
        }
        let endAt = Date(timeIntervalSince1970: endAtMs / 1000)
        armedTitle = call.getString("title") ?? "Rest over"
        armedBody = call.getString("body") ?? "Next set."

        let alreadyArmed = armedEndAt.map {
            player != nil && abs($0.timeIntervalSince(endAt)) < Self.earlyToleranceSeconds
        } ?? false
        if alreadyArmed {
            call.resolve(["scheduled": true, "unchanged": true])
            return
        }

        let ok = arm(for: endAt, force: false)
        call.resolve(["scheduled": ok, "inSeconds": endAt.timeIntervalSinceNow])
    }

    private func standDown() {
        player?.stop()
        player = nil
        armedEndAt = nil
        deactivate()
        cancelLocalNotification()
    }

    @objc func cancel(_ call: CAPPluginCall) {
        standDown()
        call.resolve()
    }

    @objc func beepNow(_ call: CAPPluginCall) {
        playImmediately()
        call.resolve()
    }

    private func playImmediately() {
        player?.stop()
        // Nothing is pending after an immediate beep, so the finish delegate must
        // not mistake this for an armed one arriving early.
        armedEndAt = nil
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
    ///
    /// Unless it was early. `play(atTime:)` runs on the audio hardware clock, and
    /// that clock's zero point moves whenever the hardware idles — so a beep can
    /// arrive well before the rest is actually over. The wall clock is the
    /// authority on whether the rest has ended; if it disagrees, this firing was
    /// the clock slipping, and the right response is to re-arm for what is
    /// genuinely left rather than to call the rest done.
    public func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        if let target = armedEndAt,
           Date() < target.addingTimeInterval(-Self.earlyToleranceSeconds) {
            // Let the music back up for the gap, then re-arm on the remainder.
            stopDucking()
            arm(for: target, force: true)
            return
        }
        armedEndAt = nil
        // Drop ducking immediately so music comes back, then stand the session
        // down — the rest is over, so nothing needs the audio clock any more.
        stopDucking()
        deactivate()
    }
}
