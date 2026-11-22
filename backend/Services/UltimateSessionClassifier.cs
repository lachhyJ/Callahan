namespace Callahan.Api.Services;

// Picks an Ultimate session type for a freshly synced Garmin activity. The
// title (stored in Notes) decides first because Lachlan's own naming is more
// specific than a time slot; the usual club slot only applies when the title
// matches nothing. Returns a session type *name*; the caller resolves it to a
// row and never calls this for an activity that already has a tag.
public static class UltimateSessionClassifier
{
    // Order matters: "Throws- Solo" is a Throws session, and the two field
    // sessions come first because "Field 1 pod" is Field 1, not a pod. The
    // bare word "training" is deliberately absent (would catch generic titles).
    // The frontend picker's suggestion mirrors this list - see
    // ultimateSessionSuggestion.js, which a test keeps in step.
    public static readonly IReadOnlyList<(string Keyword, string Name)> TitleKeywords = new[]
    {
        ("field 1", "Field 1"),
        ("field 2", "Field 2"),
        ("throws", "Throws"),
        ("solo", "Solo"),
        ("pod", "Pod"),
        ("club", "Club Training"),
        ("chilly training", "Club Training"),
        ("game", "Game"),
    };

    public const string ClubTraining = "Club Training";

    // Start-time slots (local wall clock) and duration the club sessions run.
    // Tolerances are deliberately loose on start (late arrivals, watch started
    // after warmup) and tighter on duration, which is what separates a club
    // session from a short solo run at the same hour.
    private static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMinutes(30);

    private static readonly (DayOfWeek Day, TimeSpan Start, TimeSpan Duration)[] ClubSlots =
    {
        (DayOfWeek.Wednesday, new TimeSpan(19, 0, 0), TimeSpan.FromHours(2)),
        (DayOfWeek.Sunday, new TimeSpan(12, 0, 0), TimeSpan.FromHours(3)),
        (DayOfWeek.Sunday, new TimeSpan(15, 0, 0), TimeSpan.FromHours(3)),
    };

    public static string? ByTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        foreach (var (keyword, name) in TitleKeywords)
            if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return name;
        return null;
    }

    public static bool FitsClubSlot(DateTime? startLocal, int durationSeconds)
    {
        if (startLocal is null) return false;
        var s = startLocal.Value;
        var duration = TimeSpan.FromSeconds(durationSeconds);
        foreach (var slot in ClubSlots)
        {
            if (s.DayOfWeek != slot.Day) continue;
            if ((s.TimeOfDay - slot.Start).Duration() > StartTolerance) continue;
            if ((duration - slot.Duration).Duration() > DurationTolerance) continue;
            return true;
        }
        return false;
    }

    // inTournament: tournament weekends are games, never club sessions, so the
    // time-slot fallback is skipped (a title can still say otherwise).
    public static string? Classify(string? title, DateTime? startLocal, int durationSeconds, bool inTournament)
    {
        var byTitle = ByTitle(title);
        if (byTitle is not null) return byTitle;
        if (!inTournament && FitsClubSlot(startLocal, durationSeconds))
            return ClubTraining;
        return null;
    }
}
