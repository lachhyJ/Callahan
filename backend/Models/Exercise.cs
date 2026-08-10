namespace Callahan.Api.Models;

public class Exercise
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ExerciseCategory Category { get; set; }

    public ICollection<ExerciseSet> Sets { get; set; } = [];
    public ICollection<ExerciseMuscleTarget> MuscleTargets { get; set; } = [];
}
