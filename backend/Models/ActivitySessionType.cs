namespace Callahan.Api.Models;

public class ActivitySessionType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ActivityType ActivityType { get; set; }
    public int SortOrder { get; set; }
}
