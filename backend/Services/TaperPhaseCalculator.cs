using Callahan.Api.Models;

namespace Callahan.Api.Services;

// TargetPct is the share of usual weekly volume to aim for. KeepPct and
// AccessoryPct are the per-exercise set suggestions on the workout screen: KeepPct
// for slots flagged KeepInTaper (main lifts and jumps), AccessoryPct for the rest,
// where 0 means skip it today. All three are null outside a taper.
public record TaperPhaseResult(string Phase, decimal? TargetPct, string Message, decimal? KeepPct = null, decimal? AccessoryPct = null);

// Single source of truth for the taper phase logic, shared by
// TaperController.GetRecommendation and TaperConsultService's context assembly so
// the two never drift out of sync. The shapes follow program v4's "Tapers" table,
// one per TaperProfile.
public static class TaperPhaseCalculator
{
    // A Nationals taper's last stretch: v4's "final week".
    public const int FinalDays = 7;

    public static int DefaultDays(TaperProfile profile) => profile == TaperProfile.Nationals ? 14 : 5;

    public static TaperPhaseResult Compute(int daysUntil, int taperDays, TaperProfile profile, string? eventName)
    {
        var name = eventName ?? "your tournament";
        var daysOut = $"{daysUntil} day{(daysUntil == 1 ? "" : "s")} out";

        if (daysUntil > taperDays)
        {
            return new TaperPhaseResult("build", null,
                $"{daysUntil} days until {name} — normal training, the taper starts {taperDays} days out.");
        }

        if (daysUntil == 0)
        {
            return new TaperPhaseResult("game_day", 0m,
                $"Game day — {name} is today. Rest or light activation only.");
        }

        if (profile == TaperProfile.Regionals)
        {
            return new TaperPhaseResult("taper", 0.7m,
                $"Taper — {daysOut}. Volume down about 30%: same loads, keep all jump and speed work.",
                KeepPct: 0.7m, AccessoryPct: 0.7m);
        }

        if (daysUntil <= FinalDays)
        {
            return new TaperPhaseResult("final_days", 0.5m,
                $"Final days — {daysOut}. Two gym sessions this week at half volume, main lifts and jumps only, and one short field session. Ankle circuit unchanged. Freshness, not fitness.",
                KeepPct: 0.5m, AccessoryPct: 0m);
        }

        return new TaperPhaseResult("taper", 0.7m,
            $"Taper — {daysOut}. Volume down about 30% with loads kept high: cut accessories, keep main lifts and all jump work. Field 1 reps down a third.",
            KeepPct: 1m, AccessoryPct: 0m);
    }

    // The deepest planned cut for a taper of this profile, as a reduction
    // (1 - the lowest TargetPct before game day). Fixed when the taper is set so a
    // finished taper keeps a stable planned figure to compare actuals against.
    public static decimal PlannedReduction(TaperProfile profile) => profile == TaperProfile.Nationals ? 0.5m : 0.3m;

    // Check-in/debrief window: taper start through 3 days after the event
    // (covers daily check-ins plus the day+1/day+3 debrief touchpoints).
    // One definition shared by TaperController, TaperConsultService, and
    // TaperReminderService so the window can't drift out of sync between them.
    public static (DateOnly Start, DateOnly End) CheckInWindow(DateOnly eventDate, int taperDays) =>
        (eventDate.AddDays(-taperDays), eventDate.AddDays(3));

    // Applies a requested taper to a tournament, keeping TaperDays, TaperProfile and
    // PlannedReductionPercent in step. No profile means no taper. Days default to
    // the profile's v4 length when not given. The planned figure is only
    // recomputed when the taper actually changes, so editing a tournament's name or
    // dates doesn't move the number a finished taper is measured against.
    public static void Apply(Tournament t, TaperProfile? profile, int? requestedDays)
    {
        int? days = profile is null ? null
            : requestedDays is > 0 ? requestedDays : DefaultDays(profile.Value);
        if (days == t.TaperDays && profile == t.TaperProfile) return;

        t.TaperDays = days;
        t.TaperProfile = profile;
        t.PlannedReductionPercent = profile is null ? null : PlannedReduction(profile.Value);
    }

    // "Regionals" / "Nationals" from the API, case-insensitive; anything else
    // (null, "", "None") is no taper.
    public static TaperProfile? Parse(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _)
            && Enum.TryParse<TaperProfile>(value, ignoreCase: true, out var p) && Enum.IsDefined(p) ? p : null;
}
