namespace Callahan.Api.Models;

public enum PlanSlotStatus
{
    // No manual call has been made: the tick is whatever the logged data says.
    Auto,
    // Marked done by hand. Wins over the derived answer - this is the escape
    // hatch for a session logged under the wrong template, on the wrong day, or
    // as a custom workout, all of which the matcher would otherwise call missed.
    Done,
    // Deliberately not doing it this week. Distinct from "not done yet": a
    // dropped Gym 2 in a short week is the program working as intended, and
    // should not read as a failure.
    Skipped
}
