namespace Callahan.Api.Models;

// A prescribed routine that is deliberately not a workout: the Jump Block and
// the daily ankle circuit. Both are part of the program, both need their
// instructions somewhere findable, and neither produces sets and reps worth
// recording.
//
// Kept out of WorkoutTemplate on purpose. A routine that ran through the
// workout flow would land in Streaks, in weekly consistency and in the monthly
// reports' planned-vs-actual as if it were a fourth gym session, and every one
// of those would need teaching to exclude it. Kept out of Activity for the same
// reason from the other direction - ActivityType is branched on in 38 places,
// and the aggregates that mean "all activities" would quietly fold a
// fifteen-minute session at home into run volume and the taper baseline.
//
// Deliberately not a habit tracker: no streaks, no reminders, no scoring. If it
// grows those it has become the parked habit-tracker feature and should be
// treated as that decision, not as an extension of this one.
public class Routine
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // What it is for, one line, shown under the name.
    public required string Purpose { get; set; }

    // When to do it, in the program's own words - "Every day, including rest
    // days" / "2x weekly, before Gym 1 and Gym 3". Free text rather than a
    // schedule: nothing computes against it, and the program states cadence in
    // terms the app has no way to model.
    public required string Cadence { get; set; }

    // The standing instructions that are not a per-item prescription: the
    // introduction ramp, the progression ladder, the reason it is not done in a
    // gym. Markdown-free plain text with blank-line paragraphs; the page
    // renders paragraphs and "- " bullets and nothing else, which is all the
    // program needs and avoids shipping a markdown parser for two records.
    public required string Notes { get; set; }

    public int SortOrder { get; set; }

    public ICollection<RoutineItem> Items { get; set; } = [];
    public ICollection<RoutineCompletion> Completions { get; set; } = [];
}
