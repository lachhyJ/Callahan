import Foundation

/// How the home screen widgets talk to the server.
///
/// A widget is its own process and free provisioning has no App Group, so it cannot
/// borrow the app's login. It carries one narrow key instead (`X-Widget-Key`, baked
/// in at build time from Widget.local.xcconfig into this extension's Info.plist),
/// which unlocks only the `/api/widget/*` routes. Those routes answer 404 for a
/// wrong, missing or unconfigured key alike.
enum WidgetAPI {

    enum Outcome<Value> {
        case ok(Value)
        /// 204: the route has nothing to show right now.
        case noContent
        /// 404: the server did not accept the key (wrong, or not set on the server).
        case rejected
        /// No key or base URL in this build, so no request was made.
        case notConfigured
        /// Unreachable, a server error, or a response that did not decode.
        case failure(String)
    }

    /// A build setting that did not resolve survives into the plist as "$(NAME)".
    static func configValue(_ key: String) -> String? {
        guard let value = Bundle.main.object(forInfoDictionaryKey: key) as? String else { return nil }
        return value.isEmpty || value.hasPrefix("$(") ? nil : value
    }

    static func fetch<Value: Decodable>(_ path: String, as type: Value.Type) async -> Outcome<Value> {
        guard let key = configValue("WidgetKey"),
              let base = configValue("WidgetBaseURL"),
              let url = URL(string: base + path)
        else { return .notConfigured }

        var request = URLRequest(url: url, timeoutInterval: 15)
        request.setValue(key, forHTTPHeaderField: "X-Widget-Key")

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await URLSession.shared.data(for: request)
        } catch {
            return .failure("Can't reach Callahan")
        }

        guard let http = response as? HTTPURLResponse else { return .failure("No response") }
        switch http.statusCode {
        case 200:
            do {
                return .ok(try JSONDecoder().decode(Value.self, from: data))
            } catch {
                return .failure("Unexpected response")
            }
        case 204:
            return .noContent
        case 404:
            return .rejected
        default:
            return .failure("Server error \(http.statusCode)")
        }
    }

    // The last good payload is kept in this extension's own UserDefaults (not shared
    // with the app, which needs an App Group) so a dropped connection can keep
    // showing it, marked as not refreshed, instead of blanking the widget.

    static func store<Value: Encodable>(_ value: Value, key: String) {
        if let encoded = try? JSONEncoder().encode(value) {
            UserDefaults.standard.set(encoded, forKey: key)
        }
    }

    static func stored<Value: Decodable>(key: String, as type: Value.Type) -> Value? {
        guard let data = UserDefaults.standard.data(forKey: key) else { return nil }
        return try? JSONDecoder().decode(Value.self, from: data)
    }
}
