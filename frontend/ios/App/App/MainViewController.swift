import Capacitor
import UIKit

/// Registers plugins that live in the app target rather than in an npm package.
///
/// `packageClassList` in the generated capacitor.config.json looks like the place
/// to do this, but `npx cap sync` rewrites that array from the installed npm
/// plugins every time — an entry added by hand there survives until the next sync
/// and then silently stops registering, taking the Live Activity with it.
/// Registering here is immune to that.
class MainViewController: CAPBridgeViewController {
    /// A route from a `callahan://` URL that has not been applied yet, because the
    /// webview was still loading the site (a cold start from a widget tap).
    private var pendingRoute: String?
    private var loadObservation: NSKeyValueObservation?

    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(RestActivityPlugin())
        bridge?.registerPluginInstance(RestAudioPlugin())
        bridge?.registerPluginInstance(AppInfoPlugin())
        bridge?.registerPluginInstance(WidgetBridgePlugin())

        // Re-check whenever loading settles, so a route that arrived before the
        // site finished loading is applied as soon as it has.
        loadObservation = bridge?.webView?.observe(\.isLoading, options: [.new]) { [weak self] _, _ in
            DispatchQueue.main.async { self?.applyPendingRouteIfReady() }
        }
    }

    /// Opens an in-app route taken from a widget tap. `DeepLink` has already
    /// restricted it to the known set; it is checked again before it reaches
    /// JavaScript.
    func open(route: String) {
        guard DeepLink.isKnown(route: route) else { return }
        pendingRoute = route
        applyPendingRouteIfReady()
    }

    private func applyPendingRouteIfReady() {
        guard let route = pendingRoute,
              let webView = bridge?.webView,
              !webView.isLoading,
              webView.url != nil,
              let script = DeepLink.script(for: route)
        else { return }
        pendingRoute = nil
        webView.evaluateJavaScript(script)
    }
}
