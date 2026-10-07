using Callahan.Api.Models;
using Callahan.Api.Services.Calendar;

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
        bool IsManual,
        // Set only when the week is laid out from the calendar (BuildFromCalendar).
        TimeOfDay? TimeOfDay = null,
        string? CalendarUid = null);

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

    // Where the calendar put a session, and the event that says so.
    public record Placement(DateOnly Day, TimeOfDay? Part, string Uid);

    // The week laid out from the calendar: sessions with an event sit on its day,
    // the rest wait in Unplaced. Only Gym, Field and Aerobic slots are sessions -
    // the Jump Block, rest markers and the ankle circuit never get an event.
    public record CalendarWeekResult(
        DateOnly WeekStart, List<DayResult> Days, List<SlotResult> Unplaced, List<string> Warnings);

    // Unlike Build, "done" is a week-level question: a slot is done when its session
    // was logged on any day of the week (filledSlotIds, from WeekSoFarBuilder.Fill),
    // so a session done a day early or late does not read as a miss. Missed means the
    // event's day has passed and the week has no such log.
    public static CalendarWeekResult BuildFromCalendar(
        DateOnly weekStart,
        DateOnly today,
        IReadOnlyCollection<SlotInput> slots,
        IReadOnlyCollection<OverrideInput> overrides,
        IReadOnlySet<int> filledSlotIds,
        IReadOnlyDictionary<int, Placement> placements)
    {
        var byId = overrides.ToDictionary(o => o.SlotId);
        var sessions = slots
            .Where(s => s.Kind is PlanSlotKind.Gym or PlanSlotKind.Field or PlanSlotKind.Aerobic)
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .ToList();

        SlotResult ResultFor(SlotInput slot)
        {
            byId.TryGetValue(slot.SlotId, out var ov);
            placements.TryGetValue(slot.SlotId, out var placement);

            var state = ov?.Status == PlanSlotStatus.Skipped ? SlotState.Skipped
                : ov?.Status == PlanSlotStatus.Done || filledSlotIds.Contains(slot.SlotId) ? SlotState.Done
                : placement is not null && placement.Day < today ? SlotState.Missed
                : SlotState.Upcoming;

            return new SlotResult(
                slot.SlotId, slot.Label, slot.Kind, state, slot.IsOptional,
                IsMoved: false, IsManual: ov?.Status == PlanSlotStatus.Done,
                TimeOfDay: placement?.Part, CalendarUid: placement?.Uid);
        }

        var days = new List<DayResult>();
        for (var d = 0; d < DaysInWeek; d++)
        {
            var date = weekStart.AddDays(d);
            days.Add(new DayResult(
                d, date,
                sessions.Where(s => placements.TryGetValue(s.SlotId, out var p) && p.Day == date)
                    .OrderBy(s => placements[s.SlotId].Part ?? TimeOfDay.Morning).ThenBy(s => s.SlotOrder)
                    .Select(ResultFor).ToList()));
        }

        var unplaced = sessions.Where(s => !placements.ContainsKey(s.SlotId)).Select(ResultFor).ToList();

        // Nothing placed yet means a week not planned yet, not a week breaking the
        // rules - "0 field sessions" on a blank week is noise.
        var placed = sessions
            .Where(s => placements.ContainsKey(s.SlotId))
            .Select(s => (Slot: s, Day: placements[s.SlotId].Day.DayNumber - weekStart.DayNumber))
            .ToList();

        return new CalendarWeekResult(weekStart, days, unplaced, placed.Count == 0 ? [] : BuildWarnings(placed));
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
