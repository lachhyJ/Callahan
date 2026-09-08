namespace Callahan.Api.Models;

// One session-type label on an activity. An activity can carry several - a
// field session with throwing tacked onto the same Garmin recording is a
// "Club Training" + "Throws" activity. The activity's own
// ActivitySessionTypeId still names the *primary* label (drives activityLabel,
// the suggestion heuristic and the "Game" analysis gate); the primary is, by
// invariant, always also present here as a row. Extras beyond the primary are
// pure calendar/monthly-breakdown labels - nothing derives a metric from them.
public class ActivitySessionTag
{
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int ActivitySessionTypeId { get; set; }
    public ActivitySessionType SessionType { get; set; } = null!;
}
