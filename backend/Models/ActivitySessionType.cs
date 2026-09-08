namespace Callahan.Api.Models;

public class ActivitySessionType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ActivityType ActivityType { get; set; }
    public int SortOrder { get; set; }

    // Training family, independent of ActivityType. Field-family types are
    // cross-type - offered in the classify picker for both Running and Ultimate
    // activities (see ActivitiesController.UpdateSessionTypes). Defaults to Run
    // so a new Running type added later doesn't need to remember to set it.
    public SessionTypeFamily Family { get; set; } = SessionTypeFamily.Run;
}
