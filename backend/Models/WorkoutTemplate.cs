namespace Callahan.Api.Models;

public class WorkoutTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public ICollection<WorkoutTemplateExercise> Exercises { get; set; } = [];
}
