import Toybox.Communications;
import Toybox.Lang;
import Toybox.Time;

// Wraps GET /api/resttimer/current. No timer/countdown logic lives here —
// that's RestTimerState (step 4). This module only knows how to ask the
// server "what's pending" and hand back a parsed result or an error.
//
// PORTS to a full watch app unchanged.
class RestTimerClient {

    // Set true to bypass the network entirely and feed back a fixed 90s
    // countdown, for exercising RestTimerState/the DataField draw path in
    // the simulator before the backend round trip (real token, real phone)
    // is wired up. Flip back to false before any real sideload.
    private const USE_HARDCODED_RESPONSE = false;

    private var _config as Config;
    private var _pendingCallback as Method?;

    function initialize(config as Config) {
        _config = config;
        _pendingCallback = null;
    }

    // callback: method(responseCode as Number, result as RestTimerCurrentResult?) as Void
    // responseCode mirrors Communications' contract: the HTTP status, or a
    // negative BLE_* error code on a transport failure (phone out of range,
    // GCM killed, etc.) — see plan's edge-case table.
    function fetchCurrent(callback as Method) as Void {
        if (USE_HARDCODED_RESPONSE) {
            var fakeEndsAt = Time.now().add(new Time.Duration(90));
            var result = new RestTimerCurrentResult("debug-timer", fakeEndsAt, "Back Squat", "5", 2, 4);
            callback.invoke(200, result);
            return;
        }

        if (!_config.isConfigured()) {
            callback.invoke(0, null);
            return;
        }

        var url = _config.getBaseUrl() + "/api/resttimer/current";
        var options = {
            :method => Communications.HTTP_REQUEST_METHOD_GET,
            :headers => {
                "Authorization" => "Bearer " + _config.getAuthToken()
            },
            :responseType => Communications.HTTP_RESPONSE_CONTENT_TYPE_JSON
        };

        _pendingCallback = callback;
        Communications.makeWebRequest(url, null, options, method(:onReceive));
    }

    function onReceive(responseCode as Number, data as Dictionary or String or Null) as Void {
        var callback = _pendingCallback;
        _pendingCallback = null;
        if (callback == null) {
            return;
        }

        if (responseCode == 200 && data instanceof Dictionary) {
            callback.invoke(200, RestTimerCurrentResult.fromDictionary(data as Dictionary));
        } else {
            // 204 (no pending timer) and every error case share the same
            // shape here — no result. RestTimerState is what interprets a
            // "no result" differently depending on whether it was Idle or
            // Counting when this came back (see plan 1.3/2.3).
            callback.invoke(responseCode, null);
        }
    }
}
