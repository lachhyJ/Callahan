import ActivityKit
import Capacitor
import Foundation

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
        CAPPluginMethod(name: "isSupported", returnType: CAPPluginReturnPromise)
    ]

    private var currentActivity: Any?

    @objc func isSupported(_ call: CAPPluginCall) {
        if #available(iOS 16.2, *) {
            call.resolve(["supported": ActivityAuthorizationInfo().areActivitiesEnabled])
        } else {
            call.resolve(["supported": false])
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

        let attributes = RestActivityAttributes(
            exerciseName: call.getString("exerciseName") ?? "Rest",
            targetReps: call.getString("targetReps") ?? "",
            nextSetNumber: call.getInt("nextSetNumber") ?? 1,
            totalSets: call.getInt("totalSets") ?? 1
        )
        let state = RestActivityAttributes.ContentState(
            endAt: Date(timeIntervalSince1970: endAtMs / 1000),
            totalSeconds: call.getInt("totalSeconds") ?? 0
        )

        Task {
            if let existing = self.currentActivity as? Activity<RestActivityAttributes> {
                if existing.attributes == attributes {
                    await existing.update(ActivityContent(state: state, staleDate: nil))
                    call.resolve(["started": true, "updated": true])
                    return
                }
                // Different set or exercise — this activity is finished.
                await existing.end(nil, dismissalPolicy: .immediate)
                self.currentActivity = nil
            }
            do {
                let activity = try Activity.request(
                    attributes: attributes,
                    content: ActivityContent(state: state, staleDate: nil),
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
            if let existing = self.currentActivity as? Activity<RestActivityAttributes> {
                await existing.end(nil, dismissalPolicy: .immediate)
                self.currentActivity = nil
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
