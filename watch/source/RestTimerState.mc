import Toybox.Lang;
import Toybox.Time;
import Toybox.Attention;

// The poll/countdown state machine described in plan 2.3. No UI, no direct
// networking (that's RestTimerClient) — CallahanDataField just asks this
// "what should I draw right now" every ~1Hz tick.
//
// PORTS to a full watch app unchanged.
class RestTimerState {

    const STATE_IDLE = 0;
    const STATE_COUNTING = 1;

    // Data-field onUpdate runs ~1Hz while visible — piggyback on that as the
    // tick source rather than a separate Timer. Poll every 5th tick while
    // idle: one request per ~5s, not one per onUpdate call.
    private const POLL_INTERVAL_TICKS = 5;

    private var _client as RestTimerClient;
    private var _state as Number;
    private var _fetchInFlight as Boolean;
    private var _pollTickCounter as Number;
    private var _lastResponseCode as Number;

    private var _timerId as String?;
    private var _endsAtUtc as Moment?;
    private var _exerciseName as String?;
    private var _targetReps as String?;
    private var _nextSetNumber as Number?;
    private var _totalSets as Number?;

    // serverNow - deviceNow at the last successful fetch (plan 2.4). Applied
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
        if (_state == STATE_COUNTING) {
            var remaining = remainingSeconds();
            if (remaining <= 0) {
                _state = STATE_IDLE;
                _timerId = null;
                _endsAtUtc = null;
                _pollTickCounter = POLL_INTERVAL_TICKS;
                return true;
            }
            return false;
        }

        _pollTickCounter += 1;
        if (_pollTickCounter >= POLL_INTERVAL_TICKS && !_fetchInFlight) {
            _pollTickCounter = 0;
            _fetchInFlight = true;
            _client.fetchCurrent(method(:onFetchResult));
        }
        return false;
    }

    function onFetchResult(responseCode as Number, result as RestTimerCurrentResult?) as Void {
        _fetchInFlight = false;
        _lastResponseCode = responseCode;

        if (result != null) {
            // result.serverNowUtc - Time.now() (the moment this callback
            // runs) is the clock offset — this fetch is a real round trip,
            // not the hardcoded test path, so the small latency between
            // "server stamped serverNowUtc" and "this callback observes it"
            // is within the tolerance the plan accepts.
            _clockOffset = result.serverNowUtc.subtract(Time.now()) as Duration;

            // A changed timerId (or arriving from Idle) starts a fresh
            // countdown. An unchanged timerId is just a redundant confirm —
            // nothing to do, since Counting already stopped polling.
            if (_state != STATE_COUNTING || !(result.timerId.equals(_timerId))) {
                _state = STATE_COUNTING;
                _timerId = result.timerId;
                _endsAtUtc = result.endsAtUtc;
                _exerciseName = result.exerciseName;
                _targetReps = result.targetReps;
                _nextSetNumber = result.nextSetNumber;
                _totalSets = result.totalSets;
            }
            return;
        }

        // No result: either a 204 (nothing pending) or a transport/auth
        // error. Per plan 1.3, the server drops an entry ~3s before it
        // actually fires — a 204 here is NOT a cancel signal once we're
        // already Counting. Only fire()-via-tick() (hitting zero) or a
        // different timerId showing up ends a countdown.
    }

    function isCounting() as Boolean {
        return _state == STATE_COUNTING;
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

    function setLabel() as String {
        if (_nextSetNumber == null || _totalSets == null) {
            return "";
        }
        return _targetReps + " reps · Set " + _nextSetNumber + "/" + _totalSets;
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
