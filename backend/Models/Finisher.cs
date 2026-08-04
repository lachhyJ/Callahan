namespace Callahan.Api.Models;

public class Finisher
{
    public int Id { get; set; }
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int SortOrder { get; set; }
    public int TargetSets { get; set; }
    public required string TargetReps { get; set; }
}
