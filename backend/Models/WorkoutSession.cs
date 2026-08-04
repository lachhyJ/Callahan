namespace Callahan.Api.Models;

public class WorkoutSession
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    public ICollection<ExerciseSet> Sets { get; set; } = [];
}
