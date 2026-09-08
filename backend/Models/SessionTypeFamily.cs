namespace Callahan.Api.Models;

// The training family an ActivitySessionType belongs to, independent of the
// Garmin ActivityType the activity was recorded under. Field sessions in
// particular are logged sometimes as Running, sometimes as Ultimate activities
// - Family stays Field either way, which is what makes them selectable for both
// and what the calendar keys the "field" glyph off.
public enum SessionTypeFamily
{
    Run,
    Field,
    Ultimate,
}
