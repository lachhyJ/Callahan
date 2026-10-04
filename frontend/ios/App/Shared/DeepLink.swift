import Foundation

/// Turns a `callahan://` URL into an in-app route.
///
/// The scheme is reachable by any app or web page on the phone, so the only thing
/// it may ever do is navigate to one of a fixed set of routes. Matching is exact
/// and the route is looked up in a table, never built from the URL: no path, query,
/// fragment, credentials or port is accepted, and an unknown host is ignored. That
/// also keeps the string handed to the webview a constant from this file.
enum DeepLink {
    static let scheme = "callahan"

    /// URL host -> route in the React app. `workouts` is the gym session picker
    /// (the `/` route), which the Today widget opens.
    private static let routes: [String: String] = [
        "workouts": "/",
        "plan": "/plan",
        "wellness": "/wellness",
        "dashboard": "/dashboard",
    ]

    /// The URLs widgets set as their tap target. Compiled into both the app and the
    /// widget extension, so the table above and these constants cannot drift apart;
    /// the app resolves them back to routes with `route(for:)`.
    static let workouts = link("workouts")
    static let plan = link("plan")
    static let wellness = link("wellness")
    static let dashboard = link("dashboard")

    private static func link(_ host: String) -> URL {
        URL(string: "\(scheme)://\(host)")!
    }

    static func route(for url: URL) -> String? {
        guard url.scheme?.lowercased() == scheme,
              let host = url.host?.lowercased(),
              url.user == nil, url.password == nil, url.port == nil,
              url.query == nil, url.fragment == nil,
              url.path.isEmpty || url.path == "/"
        else { return nil }
        return routes[host]
    }

    static func isKnown(route: String) -> Bool {
        routes.values.contains(route)
    }

    /// JavaScript that moves the running React Router app to `route` without
    /// reloading the page. A reload would drop in-memory state (a workout in
    /// progress). `route` is only ever one of the constants above, so it is safe to
    /// interpolate; anything else yields no script.
    static func script(for route: String) -> String? {
        guard isKnown(route: route) else { return nil }
        return """
        (function () {
          if (window.location.pathname === '\(route)') return;
          window.history.pushState({}, '', '\(route)');
          window.dispatchEvent(new PopStateEvent('popstate', { state: {} }));
        })();
        """
    }
}
