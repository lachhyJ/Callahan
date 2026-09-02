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
    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(RestActivityPlugin())
    }
}
