import Toybox.Lang;
import Toybox.Time;
import Toybox.Attention;

// The poll/countdown state machine. No UI, no direct
// networking (that's RestTimerClient) — CallahanDataField just asks this
// "what should I draw right now" every ~1Hz tick.
//
// PORTS to a full watch app unchanged.
class RestTimerState {

    const STATE_IDLE = 0;
    const STATE_COUNTING = 1;
    // Countdown reached zero: show what the next set is loaded to until the
    // phone schedules the next rest. There's no "set started" signal to
    // leave on — data fields get no input — so the next timerId is the exit.
    const STATE_NEXT_SET = 2;
    // Countdown reached zero after the session's last set: show doneLabel.
    const STATE_DONE = 3;

    // Data-field onUpdate runs ~1Hz while visible — piggyback on that as the
    // tick source rather than a separate Timer. Poll every 5th tick,
    // Idle or Counting alike: one request per ~5s, not one per onUpdate
    // call. Decided 2026-09-27 over the original design's "stop polling
    // entirely while Counting" — that missed a mid-rest ±15s/skip
    // adjustment on the phone until the countdown it was already watching
    // ran out. The activity's HR/BLE radio use dwarfs one extra request
    // every 5s, so the battery case for stopping wasn't worth the miss.
    private const POLL_INTERVAL_TICKS = 5;

    // A 204 arriving while Counting is ambiguous: the server
    // also drops a timer's entry ~PushLeadSeconds (3s) before it actually
    // fires, which is not a real cancellation. Only treat a 204 as a
    // genuine skip/cancel when there's still meaningfully more than that
    // window left — otherwise let tick() finish the countdown to zero as
    // normal. Set comfortably above the 3s lead-in plus poll jitter.
    private const AMBIGUOUS_204_WINDOW_SECONDS = 8;

    private var _client as RestTimerClient;
    private var _state as Number;
    private var _fetchInFlight as Boolean;
    private var _pollTickCounter as Number;
    private var _lastResponseCode as Number;

    private var _timerId as String?;
    private var _endsAtUtc as Moment?;
    private var _exerciseName as String?;
    private var _targetReps as String?;
    private var _targetWeight as String = "";
    private var _enteredReps as String = "";
    private var _doneLabel as String = "";

    // serverNow - deviceNow at the last successful fetch. Applied
    // to remainingSeconds() so the countdown tracks the server's clock
    // rather than the watch's. Recomputed on every 200, including while
    // idle, so it self-corrects — no need to persist it across a restart.
    private var _clockOffset as Duration;

    function initialize(client as RestTimerClient) {
        _client = client;
        _state = STATE_IDLE;
        _fetchInFlight = false;
        // Poll on the very first tick rather than waiting a full interval.
        _pollTickCounter = POLL_INTERVAL_TICKS;
        _lastResponseCode = 200;
        _clockOffset = new Time.Duration(0);
    }

    // Call once per ~1Hz tick (from onUpdate). Returns true if a rest period
    // just ended this tick, so the caller can vibrate — vibration is a
    // side effect the DataField triggers, not something this class reaches
    // out and does itself.
    function tick() as Boolean {
        var justFinished = false;

        if (_state == STATE_COUNTING && remainingSeconds() <= 0) {
            _state = _doneLabel.length() > 0 ? STATE_DONE : STATE_NEXT_SET;
            _timerId = null;
            _endsAtUtc = null;
            justFinished = true;
        }

        _pollTickCounter += 1;
        if (_pollTickCounter >= POLL_INTERVAL_TICKS && !_fetchInFlight) {
            _pollTickCounter = 0;
            _fetchInFlight = true;
            _client.fetchCurrent(method(:onFetchResult));
        }

        return justFinished;
    }

    function onFetchResult(responseCode as Number, result as RestTimerCurrentResult?) as Void {
        _fetchInFlight = false;
        _lastResponseCode = responseCode;

        if (result != null) {
            // result.serverNowUtc - Time.now() (the moment this callback
            // runs) is the clock offset — this fetch is a real round trip,
            // not the hardcoded test path, so the small latency between
            // "server stamped serverNowUtc" and "this callback observes it"
            // is well under the 1s display granularity.
            _clockOffset = result.serverNowUtc.subtract(Time.now()) as Duration;

            // A changed timerId (or arriving from Idle) starts a fresh
            // countdown. An unchanged timerId is just a redundant confirm:
            // a ±15s adjust or skip on the phone reschedules under a new id.
            if (_state != STATE_COUNTING || !(result.timerId.equals(_timerId))) {
                _state = STATE_COUNTING;
                _timerId = result.timerId;
                _endsAtUtc = result.endsAtUtc;
                _exerciseName = result.exerciseName;
                _targetReps = result.targetReps;
                _targetWeight = result.targetWeight;
                _enteredReps = result.enteredReps;
                _doneLabel = result.doneLabel;
            }
            return;
        }

        // No result: either a 204 (nothing pending) or a transport/auth
        // error. A 204 while Counting is ambiguous — see
        // AMBIGUOUS_204_WINDOW_SECONDS above — so only treat it as a real
        // skip/cancel once there's clearly more than the server's own
        // early-removal window left. Close, and it's indistinguishable
        // from the natural end; tick() finishes the countdown to zero
        // either way, which is harmless within that window.
        if (_state == STATE_COUNTING && responseCode == 204 && remainingSeconds() > AMBIGUOUS_204_WINDOW_SECONDS) {
            _state = STATE_IDLE;
            _timerId = null;
            _endsAtUtc = null;
        }
    }

    function isCounting() as Boolean {
        return _state == STATE_COUNTING;
    }

    function isNextSet() as Boolean {
        return _state == STATE_NEXT_SET;
    }

    function isDone() as Boolean {
        return _state == STATE_DONE;
    }

    function doneLabel() as String {
        return _doneLabel;
    }

    // "80 kg x 8", preferring the reps typed into the row over the
    // programmed target (often a range) — same rule as the Live Activity's
    // loadedSetLine. "x", not "×": not every Garmin system font has the glyph.
    function nextSetLoad() as String {
        var reps = _enteredReps.length() > 0 ? _enteredReps : (_targetReps != null ? _targetReps : "");
        if (_targetWeight.length() > 0 && reps.length() > 0) {
            return _targetWeight + " x " + reps;
        }
        return _targetWeight.length() > 0 ? _targetWeight : reps;
    }

    // 0 is RestTimerClient's sentinel for "not configured" — CallahanDataField
    // already shows its own message for that case via Config directly, so
    // this only reports on genuine transport/server errors.
    function hasError() as Boolean {
        return _state == STATE_IDLE && _lastResponseCode != 0 && _lastResponseCode != 200 && _lastResponseCode != 204;
    }

    function remainingSeconds() as Number {
        if (_endsAtUtc == null) {
            return 0;
        }
        var correctedNow = Time.now().add(_clockOffset);
        var diff = (_endsAtUtc as Moment).subtract(correctedNow) as Duration;
        var seconds = diff.value();
        return seconds > 0 ? seconds : 0;
    }

    function exerciseName() as String {
        return _exerciseName != null ? _exerciseName : "";
    }

    static function vibrateRestOver() as Void {
        if (Attention has :vibrate) {
            Attention.vibrate([
                new Attention.VibeProfile(50, 1000),
                new Attention.VibeProfile(0, 300),
                new Attention.VibeProfile(50, 1000)
            ]);
        }
    }
}
