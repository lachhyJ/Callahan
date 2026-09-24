import Toybox.Activity;
import Toybox.Graphics;
import Toybox.Lang;
import Toybox.WatchUi;

// DISPOSABLE — the shell is rewritten if the full watch app (phase-2b in the
// backlog) ever happens. It must contain no networking and no timer
// arithmetic; those live in RestTimerClient/RestTimerState.
//
// Placeholder only, for step 3: proves the manifest/settings/Config/
// RestTimerClient skeleton actually compiles and sideloads. RestTimerState
// (the Idle/Counting/Firing machine) and the real MM:SS draw land in step 4 —
// this view intentionally does not fetch or count down yet.
class CallahanDataField extends WatchUi.DataField {

    private var _config as Config;
    private var _client as RestTimerClient;

    function initialize() {
        DataField.initialize();
        _config = new Config();
        _client = new RestTimerClient(_config);
    }

    function compute(info as Activity.Info) as Void {
    }

    function onUpdate(dc as Dc) as Void {
        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_BLACK);
        dc.clear();
        var text = _config.isConfigured() ? "Rest Timer" : "Set base URL + token";
        dc.drawText(
            dc.getWidth() / 2,
            dc.getHeight() / 2,
            Graphics.FONT_MEDIUM,
            text,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER
        );
    }
}
