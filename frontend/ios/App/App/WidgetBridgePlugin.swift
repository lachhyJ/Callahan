import Capacitor
import WidgetKit

/// Lets the webview ask WidgetKit to reload the home screen widgets.
///
/// The widgets are a separate process with no shared storage (free provisioning
/// has no App Group), so the app cannot hand them data. It can still tell the
/// system to reload their timelines, and each widget then refetches from the
/// server itself. The JS side (widgetRefresh.js) calls this on launch, on
/// returning to the foreground, and after writes that change what the widgets show.
@objc(WidgetBridgePlugin)
public class WidgetBridgePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "WidgetBridgePlugin"
    public let jsName = "WidgetBridge"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "reload", returnType: CAPPluginReturnPromise)
    ]

    @objc func reload(_ call: CAPPluginCall) {
        WidgetCenter.shared.reloadAllTimelines()
        call.resolve()
    }
}
