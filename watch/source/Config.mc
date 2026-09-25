import Toybox.Application;
import Toybox.Lang;

// Reads the v0 auth settings (base URL + a pasted 30-day JWT) from the app's
// Connect IQ settings, editable from Garmin Connect Mobile. See plan 2.2 —
// no device-token endpoint until this round trip is proven end to end.
//
// PORTS to a full watch app unchanged: same settings shape either way.
class Config {

    function getBaseUrl() as String {
        var url = Application.Properties.getValue("BaseUrl") as String?;
        return url != null ? url : "";
    }

    function getAuthToken() as String {
        var token = Application.Properties.getValue("AuthToken") as String?;
        return token != null ? token : "";
    }

    function isConfigured() as Boolean {
        return getBaseUrl().length() > 0 && getAuthToken().length() > 0;
    }
}
