namespace Callahan.Api.Models;

public class WorkoutSession
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    public int? WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public ICollection<ExerciseSet> Sets { get; set; } = [];
}
