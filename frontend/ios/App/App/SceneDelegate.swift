import UIKit
import Capacitor

class SceneDelegate: UIResponder, UIWindowSceneDelegate {
    var window: UIWindow?

    func scene(_ scene: UIScene, willConnectTo session: UISceneSession, options connectionOptions: UIScene.ConnectionOptions) {
        guard let windowScene = scene as? UIWindowScene else { return }

        window = UIWindow(windowScene: windowScene)
        // Capacitor's template builds the root controller in code and never reads
        // Main.storyboard, so a customClass set there has no effect. The subclass
        // has to be named here — it is what registers the app-target plugins.
        window?.rootViewController = MainViewController()
        window?.makeKeyAndVisible()

        SceneDelegateProxy.shared.scene(scene, willConnectTo: session, options: connectionOptions)

        // Launched by tapping a widget: the URL arrives here, not in openURLContexts.
        handleWidgetURLs(connectionOptions.urlContexts.map { $0.url })
    }

    func scene(_ scene: UIScene, openURLContexts URLContexts: Set<UIOpenURLContext>) {
        SceneDelegateProxy.shared.scene(scene, openURLContexts: URLContexts)
        handleWidgetURLs(URLContexts.map { $0.url })
    }

    /// Widget taps arrive as `callahan://` URLs. Only the first one that maps to a
    /// known route is used; see DeepLink for what is accepted.
    private func handleWidgetURLs(_ urls: [URL]) {
        guard let route = urls.lazy.compactMap({ DeepLink.route(for: $0) }).first else { return }
        (window?.rootViewController as? MainViewController)?.open(route: route)
    }

    func scene(_ scene: UIScene, continue userActivity: NSUserActivity) {
        SceneDelegateProxy.shared.scene(scene, continue: userActivity)
    }
}
