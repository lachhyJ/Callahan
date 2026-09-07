namespace Callahan.Api.Models;

// What a planned slot is, which decides how the app works out whether it
// happened. Each kind maps to exactly one place real work gets recorded.
public enum PlanSlotKind
{
    // A WorkoutSession against a specific WorkoutTemplate.
    Gym,
    // An Activity carrying a specific ActivitySessionType.
    Field,
    // A RoutineCompletion against a specific Routine.
    Routine,
    // Aerobic maintenance - deliberately loose. The program says "easy ride, or
    // full rest", and cycling isn't synced yet, so nothing satisfies this
    // automatically; it exists to be seen and ticked by hand.
    Aerobic,
    // Nothing to satisfy. Rest is in the week because leaving it out makes the
    // week read as five days rather than seven.
    Rest
}
