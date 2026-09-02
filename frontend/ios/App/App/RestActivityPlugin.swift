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
        CAPPluginMethod(name: "getState", returnType: CAPPluginReturnPromise)
    ]

    private var currentActivity: Any?
    private var currentEndAt: Date?

    override public func load() {
        // Ending is driven from JS, but iOS suspends the webview once the app is
        // backgrounded, so a rest timer that runs out while you are elsewhere
        // strands its activity until you come back. Clearing it on return is the
        // best that can be done without push updates.
        //
        // Do NOT try to pre-empt this by ending the activity on willResignActive
        // with dismissalPolicy .after(endAt): measured on iOS 26.5, that dismisses
        // immediately rather than at the date, so the countdown vanishes from both
        // the Dynamic Island and the Lock Screen the instant you leave the app —
        // which removes the feature exactly when it is meant to be useful. The
        // staleDate below is what stops a finished activity showing a dead 0:00.
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleBecomeActive),
            name: UIApplication.didBecomeActiveNotification,
            object: nil
        )
    }

    @objc private func handleBecomeActive() {
        guard #available(iOS 16.2, *) else { return }
        guard let endAt = currentEndAt, endAt <= Date() else { return }
        let activity = currentActivity as? Activity<RestActivityAttributes>
        currentActivity = nil
        currentEndAt = nil
        Task {
            await activity?.end(nil, dismissalPolicy: .immediate)
        }
    }

    @objc func isSupported(_ call: CAPPluginCall) {
        if #available(iOS 16.2, *) {
            call.resolve(["supported": ActivityAuthorizationInfo().areActivitiesEnabled])
        } else {
            call.resolve(["supported": false])
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
            if let endAt {
                call.resolve([
                    "active": true,
                    "endAt": endAt.timeIntervalSince1970 * 1000,
                    "totalSeconds": total,
                    "revision": revision
                ])
            } else {
                call.resolve(["active": false, "revision": revision])
            }
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
        guard let endAtMs = call.getDouble("endAt") else {
            call.reject("endAt is required")
            return
        }

        let sessionStartedAtMs = call.getDouble("sessionStartedAt") ?? Date().timeIntervalSince1970 * 1000
        let attributes = RestActivityAttributes(
            exerciseName: call.getString("exerciseName") ?? "Rest",
            targetReps: call.getString("targetReps") ?? "",
            targetWeight: call.getString("targetWeight") ?? "",
            nextSetNumber: call.getInt("nextSetNumber") ?? 1,
            totalSets: call.getInt("totalSets") ?? 1,
            sessionStartedAt: Date(timeIntervalSince1970: sessionStartedAtMs / 1000)
        )
        let endAt = Date(timeIntervalSince1970: endAtMs / 1000)
        let state = RestActivityAttributes.ContentState(
            endAt: endAt,
            totalSeconds: call.getInt("totalSeconds") ?? 0
        )
        self.currentEndAt = endAt
        let totalSeconds = call.getInt("totalSeconds") ?? 0
        Task { await RestTimerStore.shared.set(endAt: endAt, totalSeconds: totalSeconds) }

        Task {
            if let existing = self.currentActivity as? Activity<RestActivityAttributes> {
                if existing.attributes == attributes {
                    await existing.update(ActivityContent(state: state, staleDate: endAt))
                    call.resolve(["started": true, "updated": true])
                    return
                }
                // Different set or exercise — this activity is finished.
                await existing.end(nil, dismissalPolicy: .immediate)
                self.currentActivity = nil
                self.currentEndAt = nil
            }
            do {
                // currentActivity only knows about activities this process
                // started, so after a relaunch mid-rest the previous launch's
                // activity is still live and invisible to us — requesting another
                // stacks a second card on the Lock Screen, one per restart. Clear
                // any strays first so there is exactly one.
                for stray in Activity<RestActivityAttributes>.activities {
                    await stray.end(nil, dismissalPolicy: .immediate)
                }
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
