namespace Callahan.Api.Models;

// One slot in the program's default week. Seeded content: this is the program's
// shape, and the app has no editor for it - a change here is a program change,
// which goes through a migration like the templates do.
//
// The week a slot sits in by default can be overridden per week (see
// PlanSlotWeek). That is the whole point: casual shift work means the real week
// moves, and a planner that can't follow it is a planner you stop opening.
public class PlanSlot
{
    public int Id { get; set; }

    // 0 = Monday ... 6 = Sunday, matching the app's Monday-first week
    // (dateUtils.startOfWeek and the Calendar grid).
    public int DefaultDayOfWeek { get; set; }

    // Order within a day, for days carrying more than one slot (the Jump Block
    // sits before Gym 1 and Gym 3 - it is done before you leave the house).
    public int SlotOrder { get; set; }

    public PlanSlotKind Kind { get; set; }
    public required string Label { get; set; }

    // Optional slots don't count as missed when they don't happen. The Sunday
    // aerobic slot is "easy ride, or full rest" - both are the program.
    public bool IsOptional { get; set; }

    // Exactly one of these is set, matching Kind; all null for Rest.
    public int? WorkoutTemplateId { get; set; }
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public int? ActivitySessionTypeId { get; set; }
    public ActivitySessionType? ActivitySessionType { get; set; }

    public int? RoutineId { get; set; }
    public Routine? Routine { get; set; }

    public ICollection<PlanSlotWeek> Weeks { get; set; } = [];
}
