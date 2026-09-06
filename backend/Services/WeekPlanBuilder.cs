using Callahan.Api.Models;

namespace Callahan.Api.Services;

// Lays out a planned week and works out what happened. Pure: everything it
// needs is passed in, so the week's behaviour can be tested without a database
// or a clock.
//
// The warnings are the reason the week is movable rather than fixed. They check
// the week as actually laid out, against the program's own spacing rules - a
// fixed week would make them constants that can never fire.
public static class WeekPlanBuilder
{
    public const int DaysInWeek = 7;

    public record SlotInput(
        int SlotId,
        int DefaultDayOfWeek,
        int SlotOrder,
        PlanSlotKind Kind,
        string Label,
        bool IsOptional,
        int? WorkoutTemplateId,
        int? ActivitySessionTypeId,
        int? RoutineId,
        // Null for anything that isn't a gym slot.
        LowerBodyLoad? LowerBodyLoad);

    public record OverrideInput(int SlotId, int? DayOfWeek, PlanSlotStatus Status);

    // What actually got logged, already narrowed to the week.
    public record LoggedInput(
        IReadOnlyCollection<(DateOnly Date, int TemplateId)> GymSessions,
        IReadOnlyCollection<(DateOnly Date, int SessionTypeId)> Activities,
        IReadOnlyCollection<(DateOnly Date, int RoutineId)> RoutineCompletions);

    public enum SlotState { Upcoming, Done, Missed, Skipped, Rest }

    public record SlotResult(
        int SlotId,
        string Label,
        PlanSlotKind Kind,
        SlotState State,
        bool IsOptional,
        bool IsMoved,
        // True when the state came from a manual call rather than logged data -
        // the page says so, because a tick you made yourself and a tick the app
        // worked out are different kinds of claim.
        bool IsManual);

    public record DayResult(int DayOfWeek, DateOnly Date, List<SlotResult> Slots);

    public record WeekResult(DateOnly WeekStart, List<DayResult> Days, List<string> Warnings);

    public static WeekResult Build(
        DateOnly weekStart,
        DateOnly today,
        IReadOnlyCollection<SlotInput> slots,
        IReadOnlyCollection<OverrideInput> overrides,
        LoggedInput logged)
    {
        var byId = overrides.ToDictionary(o => o.SlotId);

        var placed = slots
            .Select(s =>
            {
                byId.TryGetValue(s.SlotId, out var ov);
                var day = ov?.DayOfWeek ?? s.DefaultDayOfWeek;
                return (Slot: s, Day: day, Override: ov);
            })
            .ToList();

        var days = new List<DayResult>();
        for (var d = 0; d < DaysInWeek; d++)
        {
            var date = weekStart.AddDays(d);
            var slotResults = placed
                .Where(p => p.Day == d)
                .OrderBy(p => p.Slot.SlotOrder)
                .Select(p => new SlotResult(
                    p.Slot.SlotId,
                    p.Slot.Label,
                    p.Slot.Kind,
                    ResolveState(p.Slot, p.Override, date, today, logged),
                    p.Slot.IsOptional,
                    p.Override?.DayOfWeek is not null && p.Override.DayOfWeek != p.Slot.DefaultDayOfWeek,
                    p.Override?.Status == PlanSlotStatus.Done))
                .ToList();

            days.Add(new DayResult(d, date, slotResults));
        }

        return new WeekResult(weekStart, days, BuildWarnings(placed.Select(p => (p.Slot, p.Day)).ToList()));
    }

    private static SlotState ResolveState(
        SlotInput slot,
        OverrideInput? ov,
        DateOnly date,
        DateOnly today,
        LoggedInput logged)
    {
        if (slot.Kind == PlanSlotKind.Rest) return SlotState.Rest;

        if (ov?.Status == PlanSlotStatus.Skipped) return SlotState.Skipped;
        if (ov?.Status == PlanSlotStatus.Done) return SlotState.Done;

        if (IsSatisfied(slot, date, logged)) return SlotState.Done;

        // Today is still upcoming - a day isn't missed until it's over.
        return date < today ? SlotState.Missed : SlotState.Upcoming;
    }

    private static bool IsSatisfied(SlotInput slot, DateOnly date, LoggedInput logged) => slot.Kind switch
    {
        PlanSlotKind.Gym => slot.WorkoutTemplateId is int t
            && logged.GymSessions.Any(g => g.Date == date && g.TemplateId == t),
        PlanSlotKind.Field => slot.ActivitySessionTypeId is int a
            && logged.Activities.Any(x => x.Date == date && x.SessionTypeId == a),
        PlanSlotKind.Routine => slot.RoutineId is int r
            && logged.RoutineCompletions.Any(c => c.Date == date && c.RoutineId == r),
        // Cycling isn't synced, so nothing can satisfy an aerobic slot
        // automatically. It's tickable by hand and never counts as missed.
        _ => false
    };

    private static List<string> BuildWarnings(List<(SlotInput Slot, int Day)> placed)
    {
        var warnings = new List<string>();

        var heavyDays = placed
            .Where(p => p.Slot.Kind == PlanSlotKind.Gym && p.Slot.LowerBodyLoad == Models.LowerBodyLoad.Heavy)
            .Select(p => (p.Day, p.Slot.Label))
            .ToList();

        var fieldDays = placed
            .Where(p => p.Slot.Kind == PlanSlotKind.Field)
            .Select(p => (p.Day, p.Slot.Label))
            .ToList();

        foreach (var heavy in heavyDays)
        {
            foreach (var field in fieldDays.Where(f => f.Day == heavy.Day + 1))
            {
                warnings.Add(
                    $"{heavy.Label} is heavy on legs and sits the day before {field.Label}. "
                    + "The program's spacing exists so a field session gets fresh legs.");
            }
        }

        foreach (var a in heavyDays)
        {
            foreach (var b in heavyDays.Where(x => x.Day == a.Day + 1))
            {
                warnings.Add($"{a.Label} and {b.Label} are on back-to-back days, and both are heavy on legs.");
            }
        }

        if (fieldDays.Count < 2)
        {
            warnings.Add(
                $"This week has {fieldDays.Count} field session{(fieldDays.Count == 1 ? "" : "s")}. "
                + "The program asks for two - Field 1 is the protected one.");
        }

        return warnings;
    }
}
