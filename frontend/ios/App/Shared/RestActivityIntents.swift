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
}

public enum RestTimerChange {
    /// `Date` for a new end time; absent when the rest was cleared.
    public static let endAtKey = "endAt"
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
@available(iOS 17.0, *)
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

@available(iOS 17.0, *)
struct SkipRestIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Skip rest"

    init() {}

    func perform() async throws -> some IntentResult {
        await RestTimerStore.shared.skip()
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
@available(iOS 17.0, *)
struct CompleteSetIntent: LiveActivityIntent {
    static var title: LocalizedStringResource = "Complete set"

    init() {}

    func perform() async throws -> some IntentResult {
        await RestTimerStore.shared.completeSet()
        return .result()
    }
}

/// The native side's view of the running rest timer.
///
/// Deliberately plain UserDefaults in the app's own container: the widget renders
/// from the activity's ContentState, which iOS delivers to it, so nothing outside
/// this process needs to read this — which is what lets us avoid App Groups.
@available(iOS 16.2, *)
actor RestTimerStore {
    static let shared = RestTimerStore()

    private let endAtKey = "callahan.rest.endAt"
    private let totalKey = "callahan.rest.totalSeconds"
    /// Sets ticked from the card that JS has not applied yet.
    private let pendingCompletionsKey = "callahan.rest.pendingCompletions"
    /// Bumped on every native mutation so JS can tell "nothing happened" from
    /// "adjusted back to the same value".
    private let revisionKey = "callahan.rest.revision"

    var endAt: Date? {
        let t = UserDefaults.standard.double(forKey: endAtKey)
        return t > 0 ? Date(timeIntervalSince1970: t) : nil
    }
    var totalSeconds: Int { UserDefaults.standard.integer(forKey: totalKey) }
    var revision: Int { UserDefaults.standard.integer(forKey: revisionKey) }
    var pendingCompletions: Int { UserDefaults.standard.integer(forKey: pendingCompletionsKey) }

    func set(endAt: Date, totalSeconds: Int) {
        UserDefaults.standard.set(endAt.timeIntervalSince1970, forKey: endAtKey)
        UserDefaults.standard.set(totalSeconds, forKey: totalKey)
    }

    func clear() {
        UserDefaults.standard.removeObject(forKey: endAtKey)
        UserDefaults.standard.removeObject(forKey: totalKey)
    }

    /// Called once JS has folded the ticked sets into its own state. Subtracts
    /// rather than zeroing, so a press that lands while the app is waking is not
    /// swallowed by the acknowledgement of the ones before it.
    func acknowledgeCompletions(_ count: Int) {
        UserDefaults.standard.set(max(0, pendingCompletions - count), forKey: pendingCompletionsKey)
    }

    private func bumpRevision() {
        UserDefaults.standard.set(revision + 1, forKey: revisionKey)
    }

    /// Tells the audio plugin the timer moved under it. Posted on the main queue
    /// because the plugin's session and player work belongs there.
    private func announce(endAt: Date?) {
        let info: [String: Any] = endAt.map { [RestTimerChange.endAtKey: $0] } ?? [:]
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
        bumpRevision()
        announce(endAt: moved)
        await updateActivities(endAt: moved, totalSeconds: total)
    }

    /// Skip ends the *rest*, not the activity: the card belongs to the workout
    /// and should stay up between sets with the countdown zeroed.
    func skip() async {
        clear()
        bumpRevision()
        announce(endAt: nil)
        for activity in Activity<RestActivityAttributes>.activities {
            var state = activity.content.state
            state.endAt = nil
            state.totalSeconds = 0
            await activity.update(ActivityContent(state: state, staleDate: nil))
        }
    }

    /// Bank the completion and advance the card to the next set, starting its
    /// rest. The card's own idea of which set is next moves immediately so the
    /// button feels like the checkbox does; JS reconciles the real set rows on
    /// its next run and re-syncs from there.
    func completeSet() async {
        UserDefaults.standard.set(pendingCompletions + 1, forKey: pendingCompletionsKey)
        bumpRevision()

        for activity in Activity<RestActivityAttributes>.activities {
            var state = activity.content.state
            let advanced = state.nextSetNumber + 1
            state.nextSetNumber = advanced
            // Past the last set there is nothing left to rest for — the card
            // says "Last set done" and the countdown stays at zero.
            if advanced <= state.totalSets, state.restSeconds > 0 {
                let end = Date().addingTimeInterval(Double(state.restSeconds))
                state.endAt = end
                state.totalSeconds = state.restSeconds
                set(endAt: end, totalSeconds: state.restSeconds)
                announce(endAt: end)
                await activity.update(ActivityContent(state: state, staleDate: end))
            } else {
                state.endAt = nil
                state.totalSeconds = 0
                clear()
                announce(endAt: nil)
                await activity.update(ActivityContent(state: state, staleDate: nil))
            }
        }
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
