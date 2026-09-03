namespace Callahan.Api.Services;

public record TaperPhaseResult(string Phase, decimal? TargetPct, string Message);

// Single source of truth for the deterministic step-taper phase logic, shared by
// TaperController.GetRecommendation and TaperConsultService's context assembly so
// the two never drift out of sync.
public static class TaperPhaseCalculator
{
    public static TaperPhaseResult Compute(int daysUntil, int taperDays, string? eventName)
    {
        var name = eventName ?? "your tournament";

        if (daysUntil > taperDays)
        {
            return new TaperPhaseResult("build", null,
                $"{daysUntil} days until {name} — normal training, taper guidance kicks in {taperDays} days out.");
        }

        if (daysUntil == 0)
        {
            return new TaperPhaseResult("game_day", 0m,
                $"Game day — {name} is today. Rest or light activation only.");
        }

        if (daysUntil <= 2)
        {
            return new TaperPhaseResult("sharpen", 0.25m,
                $"Sharpen — {daysUntil} day{(daysUntil == 1 ? "" : "s")} out. Keep sessions short and light, aim for around 25% of your usual weekly volume.");
        }

        if (daysUntil <= taperDays / 2.0)
        {
            return new TaperPhaseResult("peak_taper", 0.5m,
                $"Peak taper — {daysUntil} days out. Aim for around 50% of your usual weekly volume, hold intensity steady.");
        }

        return new TaperPhaseResult("early_taper", 0.75m,
            $"Early taper — {daysUntil} days out. Aim for around 75% of your usual weekly volume this week.");
    }

    // The deepest planned cut for a taper of this length (the "sharpen" phase,
    // always reached before game day regardless of taper length) expressed as a
    // reduction - 1 - TargetPct. Fixed at the moment TaperDays is set so a
    // finished taper keeps a stable planned figure to compare actuals against,
    // rather than one that would keep changing with "days until" if recomputed
    // live. One definition, shared by both controllers that can set TaperDays.
    public static decimal PlannedReduction(int taperDays, string? eventName) =>
        1m - (Compute(2, taperDays, eventName).TargetPct ?? 0.25m);

    // Check-in/debrief window: taper start through 3 days after the event
    // (covers daily check-ins plus the day+1/day+3 debrief touchpoints).
    // One definition shared by TaperController, TaperConsultService, and
    // TaperReminderService so the window can't drift out of sync between them.
    public static (DateOnly Start, DateOnly End) CheckInWindow(DateOnly eventDate, int taperDays) =>
        (eventDate.AddDays(-taperDays), eventDate.AddDays(3));
}
