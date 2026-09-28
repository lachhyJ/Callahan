import Toybox.Communications;
import Toybox.Lang;

// Wraps GET /api/resttimer/current. No timer/countdown logic lives here —
// that's RestTimerState. This module only knows how to ask the
// server "what's pending" and hand back a parsed result or an error.
//
// PORTS to a full watch app unchanged.
class RestTimerClient {

    private var _config as Config;
    private var _pendingCallback as Method?;

    function initialize(config as Config) {
        _config = config;
        _pendingCallback = null;
    }

    // callback: method(responseCode as Number, result as RestTimerCurrentResult?) as Void
    // responseCode mirrors Communications' contract: the HTTP status, or a
    // negative BLE_* error code on a transport failure (phone out of range,
    // GCM killed, etc.).
    function fetchCurrent(callback as Method) as Void {
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
            callback.invoke(200, RestTimerCurrentResult.fromDictionary(data as Dictionary<String, Object?>));
        } else {
            // 204 (no pending timer) and every error case share the same
            // shape here — no result. RestTimerState is what interprets a
            // "no result" differently depending on whether it was Idle or
            // Counting when this came back.
            callback.invoke(responseCode, null);
        }
    }
}
