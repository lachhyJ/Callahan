import ActivityKit
import Capacitor
import Foundation
import UIKit

/// Bridges the web app's rest timer to a Live Activity.
///
/// `sync` is deliberately idempotent — start-or-update — because the JS side
/// calls it from the same effect that mirrors the timer into localStorage. That
/// effect declares current state on every change and does not track transitions,
/// so the native side is what decides whether this is a start or an update.
///
/// An activity is only replaced (ended and restarted) when the *attributes*
/// change, i.e. you have moved on to a different exercise or set. A ± adjust
/// moves `endAt` in the content state and the system re-renders the countdown
/// on its own; we never tick it.
@objc(RestActivityPlugin)
public class RestActivityPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "RestActivityPlugin"
    public let jsName = "RestActivity"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "sync", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "end", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "isSupported", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "getState", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "ackCompletions", returnType: CAPPluginReturnPromise)
    ]

    private var currentActivity: Any?
    private var currentEndAt: Date?

    override public func load() {
        // The activity's lifetime is the workout's, and JS owns both ends of that.
        // All this does is tidy the countdown when a rest expired while the app
        // was suspended, before JS gets a chance to re-sync.
        //
        // Do NOT try to retire a finished rest by ending the activity on
        // willResignActive with dismissalPolicy .after(endAt): measured on iOS
        // 26.5, that dismisses immediately rather than at the date, so the card
        // vanishes from both the Dynamic Island and the Lock Screen the instant
        // you leave the app — removing the feature exactly when it is useful.
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleBecomeActive),
            name: UIApplication.didBecomeActiveNotification,
            object: nil
        )
    }

    /// An expired rest zeroes the countdown; it does not retire the card, which
    /// belongs to the workout and stands until the session is finished or
    /// discarded. JS re-syncs on resume anyway — this just avoids showing a dead
    /// countdown in the gap before it does.
    @objc private func handleBecomeActive() {
        guard #available(iOS 16.2, *) else { return }
        guard let endAt = currentEndAt, endAt <= Date() else { return }
        currentEndAt = nil
        Task {
            await RestTimerStore.shared.clear()
            guard let activity = self.currentActivity as? Activity<RestActivityAttributes> else { return }
            var state = activity.content.state
            state.endAt = nil
            state.totalSeconds = 0
            await activity.update(ActivityContent(state: state, staleDate: nil))
        }
    }

    /// What the native side currently believes about the rest timer.
    ///
    /// While the app is backgrounded the Live Activity's buttons are the only way
    /// to change the timer, and they cannot reach the webview's localStorage — so
    /// JS asks for this on resume and adopts it. `revision` increments on every
    /// native mutation, which distinguishes "nothing happened" from "adjusted and
    /// landed back on the same value".
    @objc func getState(_ call: CAPPluginCall) {
        guard #available(iOS 16.2, *) else {
            call.resolve(["active": false, "revision": 0])
            return
        }
        Task {
            let endAt = await RestTimerStore.shared.endAt
            let total = await RestTimerStore.shared.totalSeconds
            let revision = await RestTimerStore.shared.revision
            let pending = await RestTimerStore.shared.pendingCompletions
            if let endAt {
                call.resolve([
                    "active": true,
                    "endAt": endAt.timeIntervalSince1970 * 1000,
                    "totalSeconds": total,
                    "revision": revision,
                    "pendingCompletions": pending
                ])
            } else {
                call.resolve([
                    "active": false,
                    "revision": revision,
                    "pendingCompletions": pending
                ])
            }
        }
    }

    /// JS has folded `count` card-ticked sets into its own state. Subtracting
    /// rather than zeroing means a press that lands while the app is waking is
    /// not swallowed by the acknowledgement of the ones before it.
    @objc func ackCompletions(_ call: CAPPluginCall) {
        guard #available(iOS 16.2, *) else {
            call.resolve()
            return
        }
        let count = call.getInt("count") ?? 0
        Task {
            await RestTimerStore.shared.acknowledgeCompletions(count)
            call.resolve()
        }
    }

    @objc func sync(_ call: CAPPluginCall) {
        guard #available(iOS 16.2, *) else {
            call.resolve(["started": false, "reason": "unsupported"])
            return
        }
        guard ActivityAuthorizationInfo().areActivitiesEnabled else {
            // The user can switch Live Activities off per-app in Settings. Not an
            // error: the push notification still covers the alert.
            call.resolve(["started": false, "reason": "disabled"])
            return
        }
        // endAt is optional now: the activity belongs to the workout, so it
        // stays up between sets with the countdown zeroed rather than being torn
        // down and rebuilt every time you finish resting.
        let endAt = call.getDouble("endAt").map { Date(timeIntervalSince1970: $0 / 1000) }
        let totalSeconds = call.getInt("totalSeconds") ?? 0

        let sessionStartedAtMs = call.getDouble("sessionStartedAt") ?? Date().timeIntervalSince1970 * 1000
        let attributes = RestActivityAttributes(
            sessionStartedAt: Date(timeIntervalSince1970: sessionStartedAtMs / 1000)
        )
        let state = RestActivityAttributes.ContentState(
            endAt: endAt,
            totalSeconds: totalSeconds,
            exerciseName: call.getString("exerciseName") ?? "Workout",
            targetReps: call.getString("targetReps") ?? "",
            targetWeight: call.getString("targetWeight") ?? "",
            nextSetNumber: call.getInt("nextSetNumber") ?? 1,
            totalSets: call.getInt("totalSets") ?? 1,
            restSeconds: call.getInt("restSeconds") ?? 0
        )
        self.currentEndAt = endAt
        Task {
            if let endAt {
                await RestTimerStore.shared.set(endAt: endAt, totalSeconds: totalSeconds)
            } else {
                await RestTimerStore.shared.clear()
            }
        }

        Task {
            // Exactly one activity, always.
            //
            // currentActivity is in-memory, so it is empty after every app
            // relaunch — but ActivityKit keeps the activity itself alive across
            // relaunches, which is the whole point of it. Trusting currentActivity
            // alone therefore starts a second card per restart. Ask the system
            // what actually exists instead, adopt it if it still describes this
            // set, and end anything left over.
            let live = Activity<RestActivityAttributes>.activities
            let adopted = live.first { $0.attributes == attributes }
            for stray in live where stray.id != adopted?.id {
                await stray.end(nil, dismissalPolicy: .immediate)
            }

            if let existing = adopted {
                // Adopting rather than recreating keeps the original card, so the
                // progress bar and elapsed readout do not restart on relaunch.
                await existing.update(ActivityContent(state: state, staleDate: endAt))
                self.currentActivity = existing
                call.resolve(["started": true, "updated": true])
                return
            }

            do {
                let activity = try Activity.request(
                    attributes: attributes,
                    content: ActivityContent(state: state, staleDate: endAt),
                    pushType: nil
                )
                self.currentActivity = activity
                call.resolve(["started": true, "updated": false])
            } catch {
                call.reject("could not start Live Activity: \(error.localizedDescription)")
            }
        }
    }

    @objc func end(_ call: CAPPluginCall) {
        guard #available(iOS 16.2, *) else {
            call.resolve()
            return
        }
        Task {
            await RestTimerStore.shared.clear()
            if let existing = self.currentActivity as? Activity<RestActivityAttributes> {
                await existing.end(nil, dismissalPolicy: .immediate)
                self.currentActivity = nil
                self.currentEndAt = nil
            }
            // Also clear anything left over from a previous launch — a crash or a
            // force-quit mid-rest would otherwise strand an activity on the lock
            // screen with a countdown nothing is going to finish.
            for activity in Activity<RestActivityAttributes>.activities {
                await activity.end(nil, dismissalPolicy: .immediate)
            }
            call.resolve()
        }
    }
}
