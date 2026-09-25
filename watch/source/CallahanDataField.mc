import Toybox.Activity;
import Toybox.Graphics;
import Toybox.Lang;
import Toybox.WatchUi;

// DISPOSABLE — the shell is rewritten if the full watch app (phase-2b in the
// backlog) ever happens. It must contain no networking and no timer
// arithmetic; those live in RestTimerClient/RestTimerState. This class only
// reads RestTimerState and draws.
class CallahanDataField extends WatchUi.DataField {

    private var _config as Config;
    private var _state as RestTimerState;

    function initialize() {
        DataField.initialize();
        _config = new Config();
        _state = new RestTimerState(new RestTimerClient(_config));
    }

    function compute(info as Activity.Info) as Void {
    }

    // Called ~1Hz while the field is visible. Ticking the state machine here
    // (rather than compute()) keeps polling/countdown timing tied to actual
    // screen visibility, and lets a rest-over vibration fire in the same
    // pass that notices it, with no separate Timer needed.
    function onUpdate(dc as Dc) as Void {
        if (_config.isConfigured()) {
            var justFinished = _state.tick();
            if (justFinished) {
                RestTimerState.vibrateRestOver();
            }
        }

        dc.setColor(Graphics.COLOR_WHITE, Graphics.COLOR_BLACK);
        dc.clear();

        if (!_config.isConfigured()) {
            drawCentered(dc, "Set base URL + token", Graphics.FONT_SMALL);
        } else if (_state.hasError()) {
            drawCentered(dc, "auth", Graphics.FONT_MEDIUM);
        } else if (_state.isCounting()) {
            drawCounting(dc);
        } else {
            drawCentered(dc, "–", Graphics.FONT_LARGE);
        }
    }

    private function drawCounting(dc as Dc) as Void {
        var remaining = _state.remainingSeconds();
        var minutes = remaining / 60;
        var seconds = remaining % 60;
        var secondsStr = seconds < 10 ? "0" + seconds : seconds.toString();
        var clock = minutes.toString() + ":" + secondsStr;

        var width = dc.getWidth();
        var height = dc.getHeight();
        var exercise = _state.exerciseName();

        // Stack the clock and exercise name by their actual font heights
        // rather than fixed offsets — fixed offsets overlapped on the 965's
        // real font metrics (see the first sim run). Real layout tuning
        // (obscurity-aware onLayout, per plan 2.5) is step 5, after the
        // first real sideload; this just fixes the overlap.
        var clockFont = Graphics.FONT_NUMBER_MEDIUM;
        var exerciseFont = Graphics.FONT_XTINY;
        var clockHeight = dc.getFontHeight(clockFont);
        var gap = 4;

        if (exercise.length() > 0) {
            var exerciseHeight = dc.getFontHeight(exerciseFont);
            var totalHeight = clockHeight + gap + exerciseHeight;
            var clockY = (height - totalHeight) / 2 + clockHeight / 2;
            var exerciseY = clockY + clockHeight / 2 + gap + exerciseHeight / 2;

            dc.drawText(width / 2, clockY, clockFont, clock, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
            dc.drawText(width / 2, exerciseY, exerciseFont, exercise, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
        } else {
            dc.drawText(width / 2, height / 2, clockFont, clock, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
        }
    }

    private function drawCentered(dc as Dc, text as String, font as FontDefinition) as Void {
        dc.drawText(
            dc.getWidth() / 2,
            dc.getHeight() / 2,
            font,
            text,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER
        );
    }
}
