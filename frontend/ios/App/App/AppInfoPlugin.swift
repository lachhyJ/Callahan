import Capacitor
import Foundation
import UserNotifications

/// Answers two questions the webview cannot answer about itself, because
/// `capacitor.config.json`'s `server.url` means the JS bundle is always
/// whatever's deployed at `callahan.ljlab.online` — never the local checkout
/// a ⌘R was actually built from:
///
///  1. "Did I ⌘R the branch I think I did?" — `NativeBuildInfo`, baked in by
///     `generate_build_info.sh` at build time (see that script).
///  2. "How long until this free-provisioning build stops launching?" — read
///     at runtime from the embedded provisioning profile, since that's the
///     real deadline rather than a proxy for it, and self-disables cleanly
///     on a paid account (~1 year profile, warning never fires).
@objc(AppInfoPlugin)
public class AppInfoPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "AppInfoPlugin"
    public let jsName = "AppInfo"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "getStatus", returnType: CAPPluginReturnPromise)
    ]

    private static let notificationID = "callahan.provisioning.expiry"
    /// How far ahead of the real deadline to warn. The banner (JS side) gives
    /// itself an extra day of lead over this — this is the load-bearing
    /// notice, since once the profile expires the app cannot open at all and
    /// an in-app banner becomes unreachable at exactly the moment it would
    /// matter.
    private static let warnLeadDays: TimeInterval = 2

    override public func load() {
        scheduleExpiryNotificationIfNeeded()
    }

    @objc func getStatus(_ call: CAPPluginCall) {
        var result: [String: Any] = [
            "branch": NativeBuildInfo.branch,
            "commit": NativeBuildInfo.commit,
            "dirty": NativeBuildInfo.dirty,
            "builtAt": NativeBuildInfo.builtAt
        ]
        // Omitted rather than `NSNull()` when absent — plain JSON `undefined`
        // on the JS side, which the falsy checks there already expect.
        if let expiresAt = Self.readProvisioningExpiry() {
            result["provisioningExpiresAt"] = ISO8601DateFormatter().string(from: expiresAt)
        }
        call.resolve(result)
    }

    /// Runs on every launch so the schedule self-corrects even if the app
    /// wasn't opened in a while. `add` on an already-pending identifier just
    /// replaces it, so re-arming daily is cheap and idempotent.
    private func scheduleExpiryNotificationIfNeeded() {
        guard let expiresAt = Self.readProvisioningExpiry() else { return }
        let fireAt = expiresAt.addingTimeInterval(-Self.warnLeadDays * 24 * 60 * 60)
        let seconds = fireAt.timeIntervalSinceNow
        guard seconds > 0 else { return }

        let centre = UNUserNotificationCenter.current()
        centre.requestAuthorization(options: [.alert, .sound]) { granted, _ in
            guard granted else { return }
            let content = UNMutableNotificationContent()
            content.title = "Callahan needs a rebuild soon"
            content.body = "Free-provisioning signing expires in a couple of days — open Xcode and ⌘R to keep it launching."
            content.interruptionLevel = .timeSensitive

            let request = UNNotificationRequest(
                identifier: Self.notificationID,
                content: content,
                trigger: UNTimeIntervalNotificationTrigger(timeInterval: seconds, repeats: false)
            )
            centre.removePendingNotificationRequests(withIdentifiers: [Self.notificationID])
            centre.add(request)
        }
    }

    /// `embedded.mobileprovision` is a CMS/PKCS7 blob wrapping a plist — not
    /// itself a plist, so `PropertyListSerialization` can't read the file
    /// directly. The plist text is embedded verbatim inside the CMS envelope
    /// though, so it's extractable by slicing between the first `<?xml` and
    /// the matching `</plist>` byte ranges and parsing just that span.
    ///
    /// Returns `nil` on anything unexpected — no embedded profile (always
    /// true in the Simulator, which isn't code-signed with one at all), a
    /// format that doesn't parse, a missing `ExpirationDate` key. This is the
    /// one piece of this feature that can't be exercised ahead of an actual
    /// on-device build, so it fails closed rather than guessing.
    private static func readProvisioningExpiry() -> Date? {
        guard let url = Bundle.main.url(forResource: "embedded", withExtension: "mobileprovision"),
              let data = try? Data(contentsOf: url),
              let xmlStart = data.range(of: Data("<?xml".utf8)),
              let plistEnd = data.range(of: Data("</plist>".utf8), in: xmlStart.lowerBound..<data.endIndex)
        else { return nil }

        let plistData = data.subdata(in: xmlStart.lowerBound..<plistEnd.upperBound)
        guard let plist = try? PropertyListSerialization.propertyList(from: plistData, format: nil) as? [String: Any] else {
            return nil
        }
        return plist["ExpirationDate"] as? Date
    }
}
