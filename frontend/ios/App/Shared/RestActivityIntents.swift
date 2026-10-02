import ActivityKit
import AppIntents
import Foundation

/// Posted whenever the native side moves or clears the rest timer behind JS's
/// back, so the audio plugin can re-arm the beep it has already scheduled.
///
/// The Live Activity's buttons run in this process while the webview is
/// suspended, so JS cannot be told at the time — and without this the beep stayed
/// pinned to the pre-adjustment time while the countdown moved.
public extension Notification.Name {
    static let callahanRestTimerChanged = Notification.Name("callahan.rest.changed")

    /// Posted by RestAudioPlugin the moment its armed beep genuinely finishes
    /// (wall clock confirmed, not an early audio-clock firing that gets
    /// re-armed) — the audio session's keep-alive is what has kept this
    /// process from being suspended, so this is the one reliably-firing signal
    /// that a rest is actually over regardless of whether the phone is locked.
    /// RestActivityPlugin listens for this to retire the countdown into the
    /// Live Activity's next-set display right then, rather than waiting for
    /// the app to next become active.
    static let callahanRestBeepFinished = Notification.Name("callahan.rest.beepFinished")

    /// A plain diagnostic line for RestAudioPlugin's diary, posted by code
    /// outside that plugin (e.g. RestActivityPlugin) that has nothing else to
    /// write to. See RestAudioPlugin's own Diary section for why this exists —
    /// the same "guessing costs a whole workout per attempt" reasoning applies
    /// to the Live Activity's own countdown-retiring path, which has no
    /// visibility on-device otherwise.
    static let callahanDiaryEvent = Notification.Name("callahan.diary.event")
}

public enum CallahanDiary {
    public static let messageKey = "message"
}

public enum RestTimerChange {
    /// `Date` for a new end time; absent when the rest was cleared.
    public static let endAtKey = "endAt"
    /// Names which native path cleared/moved the timer — added 2026-09-23
    /// chasing a "silent miss" report where a rest got torn down ~45s into
    /// its window with no diary line explaining why. `standDown()`,
    /// `playImmediately()`'s callers were already tagged for the earlier
    /// duplicate-beep bug; this closes the matching gap on the clear side.
    public static let reasonKey = "reason"
}

/// The -15s / +15s / Skip / Tick buttons on the Live Activity.
///
/// A LiveActivityIntent runs in the *app's* process — iOS launches the app in the
/// background if it is not already running — so these can touch UserDefaults and
/// ActivityKit directly without an App Group entitlement (free provisioning cannot
/// add one). The widget extension only needs the type to exist to declare the
/// button; it never executes the body.
///
/// While an activity is live the native side is authoritative for `endAt`: the
/// webview is suspended in the background and cannot be told about a button press
/// at the time it happens. The JS reconciles from RestTimerStore when it next
/// runs — see RestActivityPlugin.getState().
struct AdjustRestIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Adjust rest"

    @Parameter(title: "Seconds")
    var deltaSeconds: Int

    init() {}
    init(deltaSeconds: Int) { self.deltaSeconds = deltaSeconds }

    func perform() async throws -> some IntentResult {
        await RestTimerStore.shared.adjust(by: deltaSeconds)
        return .result()
    }
}

struct SkipRestIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Skip rest"

    init() {}

    func perform() async throws -> some IntentResult {
        await RestTimerStore.shared.skip(reason: "SkipRestIntent")
        return .result()
    }
}

/// Tick the set you just did, from the card, and start the next rest — the same
/// thing the checkbox in the app does, for the case the whole feature exists for:
/// the phone is locked and the rest has just run out.
///
/// The card can only advance its own display; the actual set row lives in the
/// webview's state, so the completion is banked as a count here and applied when
/// JS next runs. That makes it safe to press several times across a locked
/// session without losing any of them.
struct CompleteSetIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Complete set"

    init() {}

    func perform() async throws -> some IntentResult {
        await RestTimerStore.shared.completeSet(reason: "CompleteSetIntent")
        return .result()
    }
}

/// The native side's view of the running rest timer.
///
/// Deliberately plain UserDefaults in the app's own container: the widget renders
/// from the activity's ContentState, which iOS delivers to it, so nothing outside
/// this process needs to read this — which is what lets us avoid App Groups.
actor RestTimerStore {
    static let shared = RestTimerStore()

    private let endAtKey = "callahan.rest.endAt"
    private let totalKey = "callahan.rest.totalSeconds"
    /// Sets ticked from the card that JS has not applied yet.
    private let pendingCompletionsKey = "callahan.rest.pendingCompletions"
    /// What the card's buttons need to reach the server on their own (the Garmin
    /// field polls it, and the webview that normally keeps it current is
    /// suspended while the phone is locked). Handed down by JS on every sync.
    private let serverBaseKey = "callahan.rest.serverBase"
    private let authTokenKey = "callahan.rest.authToken"
    /// The server-side timer that currently mirrors this rest. Absent means the
    /// server is not known to match, so JS must re-sync it on its next run.
    private let serverTimerIdKey = "callahan.rest.serverTimerId"

    var endAt: Date? {
        let t = UserDefaults.standard.double(forKey: endAtKey)
        return t > 0 ? Date(timeIntervalSince1970: t) : nil
    }
    var totalSeconds: Int { UserDefaults.standard.integer(forKey: totalKey) }
    var pendingCompletions: Int { UserDefaults.standard.integer(forKey: pendingCompletionsKey) }
    var serverTimerId: String? { UserDefaults.standard.string(forKey: serverTimerIdKey) }

    func setServer(base: String, token: String) {
        UserDefaults.standard.set(base, forKey: serverBaseKey)
        UserDefaults.standard.set(token, forKey: authTokenKey)
    }

    func setServerTimerId(_ id: String?) {
        if let id { UserDefaults.standard.set(id, forKey: serverTimerIdKey) }
        else { UserDefaults.standard.removeObject(forKey: serverTimerIdKey) }
    }

    /// Bring the server's pending timer into line with a rest the card just
    /// moved, so the watch does not wait for the webview to wake. Best effort:
    /// any failure leaves `serverTimerId` unset, and JS re-syncs on resume as it
    /// always did. Cancels the previous timer only once the replacement exists,
    /// so the watch is never left with nothing to poll.
    ///
    /// `endAt == nil` means the rest is over: just cancel.
    func syncServerTimer(state: RestActivityAttributes.ContentState, endAt: Date?) async {
        let old = serverTimerId
        setServerTimerId(nil)
        let defaults = UserDefaults.standard
        guard let base = defaults.string(forKey: serverBaseKey), !base.isEmpty,
              let token = defaults.string(forKey: authTokenKey), !token.isEmpty else { return }

        if let endAt {
            let remaining = max(1, Int(endAt.timeIntervalSinceNow.rounded()))
            let body: [String: Any] = [
                "durationSeconds": remaining,
                "exerciseName": state.exerciseName,
                "targetReps": state.targetReps,
                "targetWeight": state.targetWeight,
                "enteredReps": state.enteredReps,
                "nextSetNumber": state.nextSetNumber,
                "totalSets": state.totalSets,
                "warmupSets": state.warmupSets ?? 0,
                // Native audio sounds the alert on the device clock; a server
                // push as well would double it (see scheduleRestTimer's caller).
                "suppressPush": true,
                "doneLabel": state.doneLabel
            ]
            if let data = await Self.post(base: base, token: token, path: "/api/resttimer/schedule", body: body),
               let id = (try? JSONSerialization.jsonObject(with: data) as? [String: Any])?["timerId"] as? String {
                setServerTimerId(id)
            }
        }
        if let old {
            _ = await Self.post(base: base, token: token, path: "/api/resttimer/cancel/\(old)", body: nil)
        }
    }

    /// Short timeout: the intent is only given a few seconds of background time,
    /// and the card has already been updated by the time this runs.
    private static func post(base: String, token: String, path: String, body: [String: Any]?) async -> Data? {
        guard let url = URL(string: base + path) else { return nil }
        var req = URLRequest(url: url, timeoutInterval: 6)
        req.httpMethod = "POST"
        req.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        if let body {
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
            req.httpBody = try? JSONSerialization.data(withJSONObject: body)
        }
        guard let (data, resp) = try? await URLSession.shared.data(for: req),
              let http = resp as? HTTPURLResponse, (200..<300).contains(http.statusCode) else { return nil }
        return data
    }

    func set(endAt: Date, totalSeconds: Int) {
        UserDefaults.standard.set(endAt.timeIntervalSince1970, forKey: endAtKey)
        UserDefaults.standard.set(totalSeconds, forKey: totalKey)
    }

    func clear() {
        UserDefaults.standard.removeObject(forKey: endAtKey)
        UserDefaults.standard.removeObject(forKey: totalKey)
    }

    /// Clear the timer *and* tell the audio plugin about it.
    ///
    /// Plain `clear()` is silent, which is right for the callers that announce
    /// separately — but the workout-ended path used it directly, so an armed
    /// beep survived the session it belonged to and sounded after the workout
    /// had been saved. Anything that ends a rest for good should use this.
    func standDown(reason: String = "RestTimerStore.standDown") {
        clear()
        setServerTimerId(nil)
        announce(endAt: nil, reason: reason)
    }

    /// Called once JS has folded the ticked sets into its own state. Subtracts
    /// rather than zeroing, so a press that lands while the app is waking is not
    /// swallowed by the acknowledgement of the ones before it.
    func acknowledgeCompletions(_ count: Int) {
        UserDefaults.standard.set(max(0, pendingCompletions - count), forKey: pendingCompletionsKey)
    }

    /// Tells the audio plugin the timer moved under it. Posted on the main queue
    /// because the plugin's session and player work belongs there.
    private func announce(endAt: Date?, reason: String) {
        var info: [String: Any] = endAt.map { [RestTimerChange.endAtKey: $0] } ?? [:]
        info[RestTimerChange.reasonKey] = reason
        Task { @MainActor in
            NotificationCenter.default.post(
                name: .callahanRestTimerChanged, object: nil, userInfo: info
            )
        }
    }

    func adjust(by deltaSeconds: Int) async {
        guard let current = endAt else { return }
        // Never let a -15 push the end into the past; that would render as a
        // finished timer the app has no way to reconcile sensibly.
        let moved = max(Date().addingTimeInterval(1), current.addingTimeInterval(Double(deltaSeconds)))
        let total = max(totalSeconds, Int(moved.timeIntervalSince(Date())))
        set(endAt: moved, totalSeconds: total)
        announce(endAt: moved, reason: "AdjustRestIntent")
        await updateActivities(endAt: moved, totalSeconds: total)
        if let state = Activity<RestActivityAttributes>.activities.first?.content.state {
            await syncServerTimer(state: state, endAt: moved)
        }
    }

    /// Skip ends the *rest*, not the activity: the card belongs to the workout
    /// and should stay up between sets with the countdown zeroed.
    func skip(reason: String = "RestTimerStore.skip") async {
        clear()
        announce(endAt: nil, reason: reason)
        for activity in Activity<RestActivityAttributes>.activities {
            var state = activity.content.state
            state.endAt = nil
            state.totalSeconds = 0
            await activity.update(ActivityContent(state: state, staleDate: nil))
        }
        if let state = Activity<RestActivityAttributes>.activities.first?.content.state {
            await syncServerTimer(state: state, endAt: nil)
        }
    }

    /// Bank the completion and advance the card to the next set, starting its
    /// rest. The card's own idea of which set is next moves immediately so the
    /// button feels like the checkbox does; JS reconciles the real set rows on
    /// its next run and re-syncs from there.
    func completeSet(reason: String = "CompleteSetIntent") async {
        UserDefaults.standard.set(pendingCompletions + 1, forKey: pendingCompletionsKey)

        var synced: (state: RestActivityAttributes.ContentState, endAt: Date?)?
        for activity in Activity<RestActivityAttributes>.activities {
            var state = activity.content.state
            // A tick on a real set always rests, including an exercise's last one
            // (the rest belongs to the exercise just done — `restSeconds` here is
            // already that exercise's own). Only a tick past the end, or on a
            // finished workout, has nothing to rest for.
            let tickedRealSet = state.nextSetNumber <= state.totalSets && !state.isWorkoutDone
            let advanced = state.nextSetNumber + 1
            // Ticking the exercise's last set: read the rest length off this card
            // *before* it is repointed at whatever comes next.
            let restForTick = state.restSeconds
            let restIsLastInSuperset = state.isLastInSuperset
            if tickedRealSet, advanced > state.totalSets, let next = state.following, !next.doneLabel.isEmpty {
                // Nothing follows this exercise: the card reads the done label
                // once the rest ends, still counting past the end as before.
                state.doneLabel = next.doneLabel
                state.nextSetNumber = advanced
                state.following = nil
            } else if tickedRealSet, advanced > state.totalSets, let next = state.following {
                state.exerciseName = next.exerciseName
                state.targetReps = next.targetReps
                state.targetWeight = next.targetWeight
                state.enteredReps = next.enteredReps
                state.nextSetNumber = next.nextSetNumber
                state.totalSets = next.totalSets
                state.warmupSets = next.warmupSets
                state.restSeconds = next.restSeconds
                state.isLastInSuperset = next.isLastInSuperset
                state.following = nil
            } else {
                state.nextSetNumber = advanced
            }
            // A non-last superset member runs straight into the next exercise
            // with no rest — only the group's last member's rest stands for the
            // round (see `isLastInSuperset`'s doc comment / suppressesRest in
            // activeWorkout.js).
            if restIsLastInSuperset, tickedRealSet, restForTick > 0 {
                let end = Date().addingTimeInterval(Double(restForTick))
                state.endAt = end
                state.totalSeconds = restForTick
                set(endAt: end, totalSeconds: restForTick)
                announce(endAt: end, reason: reason)
                await activity.update(ActivityContent(state: state, staleDate: end))
                synced = (state, end)
            } else {
                state.endAt = nil
                state.totalSeconds = 0
                clear()
                announce(endAt: nil, reason: "\(reason) (no rest left)")
                await activity.update(ActivityContent(state: state, staleDate: nil))
                synced = (state, nil)
            }
        }
        // After the card has moved, so the button still feels instant.
        if let synced { await syncServerTimer(state: synced.state, endAt: synced.endAt) }
    }

    /// Moves the countdown without disturbing which set the card describes —
    /// that half of the state belongs to the app, not to these buttons.
    private func updateActivities(endAt: Date, totalSeconds: Int) async {
        for activity in Activity<RestActivityAttributes>.activities {
            var state = activity.content.state
            state.endAt = endAt
            state.totalSeconds = totalSeconds
            await activity.update(ActivityContent(state: state, staleDate: endAt))
        }
    }
}
