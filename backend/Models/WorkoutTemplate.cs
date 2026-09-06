namespace Callahan.Api.Models;

public class WorkoutTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Subtitle { get; set; }

    // Week order - the spacing the program prescribes, not a ranking. Gym 2
    // sits third because it is deliberately light on legs and lands the day
    // before Field 2.
    public int SortOrder { get; set; }

    // Retired templates stay in the database forever: sessions reference them
    // by WorkoutTemplateId and History, Streaks and the monthly reports all
    // read their names. This only hides them from the picker - GET
    // /api/workouttemplates/{id}/start still serves them, so nothing 404s.
    public bool IsRetired { get; set; }

    // See LowerBodyLoad. Planner input only; nothing in the training flow
    // reads it.
    public LowerBodyLoad LowerBodyLoad { get; set; }

    // Which session to keep when the week is short, lowest first. Distinct
    // from SortOrder: the program's priority order (Gym 1, Gym 3, Gym 2) is
    // deliberately not its week order.
    public int PriorityOrder { get; set; }

    public ICollection<WorkoutTemplateExercise> Exercises { get; set; } = [];
}
