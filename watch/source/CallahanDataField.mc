import Toybox.Activity;
import Toybox.Graphics;
import Toybox.Lang;
import Toybox.WatchUi;

// DISPOSABLE — the shell is rewritten if the full watch app (phase-2b in the
// backlog) ever happens. It must contain no networking and no timer
// arithmetic; those live in RestTimerClient/RestTimerState. This class only
// reads RestTimerState and draws.
class CallahanDataField extends WatchUi.DataField {

    // Largest-to-smallest — NUMBER_HOT is the biggest of the number-only
    // fonts, THAI_HOT bigger still but reserved for genuinely huge fields.
    // Ordering confirmed against the SDK docs (Graphics.html), not assumed:
    // an earlier version of this file had HOT and MEDIUM backwards.
    private const CLOCK_FONT_CANDIDATES = [
        Graphics.FONT_NUMBER_HOT,
        Graphics.FONT_NUMBER_MEDIUM,
        Graphics.FONT_NUMBER_MILD,
        Graphics.FONT_MEDIUM,
        Graphics.FONT_SMALL,
        Graphics.FONT_XTINY
    ] as Array<Graphics.FontType>;

    // The next-set load has letters in it ("80 kg x 8"), which the
    // number-only fonts can't render.
    private const TEXT_VALUE_FONT_CANDIDATES = [
        Graphics.FONT_LARGE,
        Graphics.FONT_MEDIUM,
        Graphics.FONT_SMALL,
        Graphics.FONT_TINY,
        Graphics.FONT_XTINY
    ] as Array<Graphics.FontType>;

    private const MESSAGE_FONT_CANDIDATES = [
        Graphics.FONT_MEDIUM,
        Graphics.FONT_SMALL,
        Graphics.FONT_XTINY
    ] as Array<Graphics.FontType>;

    // Native fields (TIMER, ELAPSED, ACTIVE CALORIES) always show a small
    // caps label above their value — added after Lachlan pointed out ours
    // didn't, and it made the field harder to place at a glance next to them.
    private const LABEL = "REST";
    private const NEXT_LABEL = "NEXT";
    private const DONE_LABEL = "WORKOUT";
    private const LABEL_FONT = Graphics.FONT_XTINY;

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
            drawCentered(dc, "Set base URL + token", MESSAGE_FONT_CANDIDATES);
        } else if (_state.hasError()) {
            drawCentered(dc, "auth", MESSAGE_FONT_CANDIDATES);
        } else if (_state.isCounting()) {
            drawCounting(dc);
        } else if (_state.isNextSet()) {
            drawNextSet(dc);
        } else if (_state.isDone()) {
            drawLabeledValue(dc, DONE_LABEL, _state.doneLabel(), TEXT_VALUE_FONT_CANDIDATES, null);
        } else {
            drawLabeledValue(dc, LABEL, "–", CLOCK_FONT_CANDIDATES, null);
        }
    }

    private function drawCounting(dc as Dc) as Void {
        var remaining = _state.remainingSeconds();
        var minutes = remaining / 60;
        var seconds = remaining % 60;
        var secondsStr = seconds < 10 ? "0" + seconds : seconds.toString();
        var clock = minutes.toString() + ":" + secondsStr;

        drawLabeledValue(dc, LABEL, clock, CLOCK_FONT_CANDIDATES, _state.exerciseName());
    }

    // Nothing loaded (fresh exercise, bodyweight with no reps typed) falls
    // back to the exercise name as the value — still says what's next.
    private function drawNextSet(dc as Dc) as Void {
        var load = _state.nextSetLoad();
        if (load.length() > 0) {
            drawLabeledValue(dc, NEXT_LABEL, load, TEXT_VALUE_FONT_CANDIDATES, _state.exerciseName());
        } else {
            drawLabeledValue(dc, NEXT_LABEL, _state.exerciseName(), TEXT_VALUE_FONT_CANDIDATES, null);
        }
    }

    // A data field's region varies a lot in both shape and size depending on
    // how many other fields share the activity screen — a 1-field slot and a
    // half-width slot in a 2-field row can have the same height but very
    // different width. Rather than guess a size class from getObscurityFlags
    // (tried first; missed the narrow-but-tall case entirely, see git log),
    // measure the actual rendered width against the field's real width every
    // draw and pick the largest font that fits.
    //
    // Stacks LABEL (always) / value (fit to width) / secondaryText (only if
    // it fits both width and remaining height) — matches the label-above-
    // value convention every native field on the same screen already uses.
    private function drawLabeledValue(dc as Dc, label as String, valueText as String, valueCandidates as Array<Graphics.FontType>, secondaryText as String?) as Void {
        var width = dc.getWidth();
        var height = dc.getHeight();
        var margin = 8;

        var labelHeight = dc.getFontHeight(LABEL_FONT);
        var labelGap = 2;

        // Reserve room for the label first — a width-only fit only checked
        // width, so it happily picked a value font tall enough to fill the
        // whole field on its own, leaving the label nowhere to go (drawn
        // off the top edge and clipped). Constrain the value font to what's
        // left after the label instead.
        var valueFont = fitFontBox(dc, valueText, valueCandidates, width - margin, height - labelHeight - labelGap);
        var valueHeight = dc.getFontHeight(valueFont);

        var secondaryFont = Graphics.FONT_XTINY;
        var secondaryGap = 4;
        var showSecondary = secondaryText != null && secondaryText.length() > 0
            && dc.getTextWidthInPixels(secondaryText, secondaryFont) <= width - margin
            && labelHeight + labelGap + valueHeight + secondaryGap + dc.getFontHeight(secondaryFont) <= height;

        var totalHeight = labelHeight + labelGap + valueHeight;
        if (showSecondary) {
            totalHeight += secondaryGap + dc.getFontHeight(secondaryFont);
        }

        // A field too small for all three lines still has room for the label
        // line itself — so when the exercise name can't go below as a third
        // line, put it in the label's place instead of the static label.
        // Which exercise the rest is for is more useful at a glance than a
        // generic word, and it's the one piece of information this field
        // shows that no native field already covers.
        var labelText = label;
        if (!showSecondary && secondaryText != null && secondaryText.length() > 0
            && dc.getTextWidthInPixels(secondaryText, LABEL_FONT) <= width - margin) {
            labelText = secondaryText;
        }

        var labelY = (height - totalHeight) / 2 + labelHeight / 2;
        var valueY = labelY + labelHeight / 2 + labelGap + valueHeight / 2;

        dc.drawText(width / 2, labelY, LABEL_FONT, labelText, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
        dc.drawText(width / 2, valueY, valueFont, valueText, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);

        if (showSecondary) {
            var secondaryHeight = dc.getFontHeight(secondaryFont);
            var secondaryY = valueY + valueHeight / 2 + secondaryGap + secondaryHeight / 2;
            dc.drawText(width / 2, secondaryY, secondaryFont, secondaryText, Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER);
        }
    }

    private function drawCentered(dc as Dc, text as String, candidates as Array<Graphics.FontType>) as Void {
        var font = fitFontBox(dc, text, candidates, dc.getWidth() - 8, dc.getHeight());
        dc.drawText(
            dc.getWidth() / 2,
            dc.getHeight() / 2,
            font,
            text,
            Graphics.TEXT_JUSTIFY_CENTER | Graphics.TEXT_JUSTIFY_VCENTER
        );
    }

    // Largest candidate that fits both maxWidth and maxHeight; falls back to
    // the smallest candidate (accepting clipping) if even that doesn't fit —
    // better than picking nothing.
    private function fitFontBox(dc as Dc, text as String, candidates as Array<Graphics.FontType>, maxWidth as Number, maxHeight as Number) as Graphics.FontType {
        for (var i = 0; i < candidates.size(); i++) {
            if (dc.getTextWidthInPixels(text, candidates[i]) <= maxWidth && dc.getFontHeight(candidates[i]) <= maxHeight) {
                return candidates[i];
            }
        }
        return candidates[candidates.size() - 1];
    }
}
