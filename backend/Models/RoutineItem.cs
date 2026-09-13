namespace Callahan.Api.Models;

// One line of a routine's prescription - the shape of the tables in the program
// document. Seed-owned: this is content, and nothing in the app writes to it.
// Contrast RoutineCompletion, which is entirely runtime-owned.
public class RoutineItem
{
    public int Id { get; set; }
    public int RoutineId { get; set; }
    public Routine Routine { get; set; } = null!;

    public int ItemOrder { get; set; }
    public required string Name { get; set; }

    // "45 secs/side", "4x3", "2x10" - or empty where the item is a step rather
    // than a dose ("Ankle circuit, abbreviated").
    public required string Prescription { get; set; }

    // The program's own Notes column, where it has one.
    public string? Cue { get; set; }

    // Null means this item has no hold timer (rep-based or step-only items).
    public int? HoldSeconds { get; set; }
    public bool IsPerSide { get; set; }

    // Gap between sides when IsPerSide is on, mirrors Exercise.PerSideDelaySeconds.
    public int? PerSideDelaySeconds { get; set; }
}
