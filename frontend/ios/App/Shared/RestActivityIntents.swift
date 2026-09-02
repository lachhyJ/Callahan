import ActivityKit
import AppIntents
import Foundation

/// The -15s / +15s / Skip buttons on the Live Activity.
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
    /// Bumped on every native mutation so JS can tell "nothing happened" from
    /// "adjusted back to the same value".
    private let revisionKey = "callahan.rest.revision"

    var endAt: Date? {
        let t = UserDefaults.standard.double(forKey: endAtKey)
        return t > 0 ? Date(timeIntervalSince1970: t) : nil
    }
    var totalSeconds: Int { UserDefaults.standard.integer(forKey: totalKey) }
    var revision: Int { UserDefaults.standard.integer(forKey: revisionKey) }

    func set(endAt: Date, totalSeconds: Int) {
        UserDefaults.standard.set(endAt.timeIntervalSince1970, forKey: endAtKey)
        UserDefaults.standard.set(totalSeconds, forKey: totalKey)
    }

    func clear() {
        UserDefaults.standard.removeObject(forKey: endAtKey)
        UserDefaults.standard.removeObject(forKey: totalKey)
    }

    private func bumpRevision() {
        UserDefaults.standard.set(revision + 1, forKey: revisionKey)
    }

    func adjust(by deltaSeconds: Int) async {
        guard let current = endAt else { return }
        // Never let a -15 push the end into the past; that would render as a
        // finished timer the app has no way to reconcile sensibly.
        let moved = max(Date().addingTimeInterval(1), current.addingTimeInterval(Double(deltaSeconds)))
        let total = max(totalSeconds, Int(moved.timeIntervalSince(Date())))
        set(endAt: moved, totalSeconds: total)
        bumpRevision()
        await updateActivities(endAt: moved, totalSeconds: total)
    }

    func skip() async {
        clear()
        bumpRevision()
        for activity in Activity<RestActivityAttributes>.activities {
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    private func updateActivities(endAt: Date, totalSeconds: Int) async {
        let state = RestActivityAttributes.ContentState(endAt: endAt, totalSeconds: totalSeconds)
        for activity in Activity<RestActivityAttributes>.activities {
            await activity.update(ActivityContent(state: state, staleDate: endAt))
        }
    }
}
