using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// Boils a planned week down to what the Today widget shows. Pure, like
// WeekPlanBuilder: the week and the date come in, so the rules can be tested with
// no database or clock.
public static class WidgetTodayBuilder
{
    // Slots the week is judged on. Optional slots (a bonus session) and rest
    // markers neither help nor hurt the count, and a slot you deliberately
    // skipped isn't something you failed to do.
    private static bool Counts(WeekPlanBuilder.SlotResult s) =>
        !s.IsOptional
        && s.State != WeekPlanBuilder.SlotState.Rest
        && s.State != WeekPlanBuilder.SlotState.Skipped;

    // `nextWeek` is only consulted when nothing is left this week, so Sunday
    // evening still points at Monday instead of showing an empty "next".
    public static WidgetTodayDto Build(
        WeekPlanBuilder.WeekResult week, WeekPlanBuilder.WeekResult? nextWeek, DateOnly today)
    {
        var todaySlots = week.Days
            .FirstOrDefault(d => d.Date == today)?.Slots
            .Select(s => new WidgetSlotDto(s.Label, s.Kind.ToString(), s.State.ToString()))
            .ToList() ?? [];

        var counted = week.Days.SelectMany(d => d.Slots).Where(Counts).ToList();

        return new WidgetTodayDto(
            today,
            todaySlots,
            FirstUpcoming(week, today) ?? (nextWeek is null ? null : FirstUpcoming(nextWeek, nextWeek.WeekStart)),
            counted.Count(s => s.State == WeekPlanBuilder.SlotState.Done),
            counted.Count,
            week.Days.Select(d => new WidgetDayDto(d.DayOfWeek, DayState(d))).ToList());
    }

    // First slot still to do, from `from` onward; today's own remaining slots count,
    // so with a session left today "next" is that session.
    private static WidgetNextDto? FirstUpcoming(WeekPlanBuilder.WeekResult week, DateOnly from)
    {
        foreach (var day in week.Days.Where(d => d.Date >= from).OrderBy(d => d.Date))
        {
            var slot = day.Slots.FirstOrDefault(s =>
                s.State == WeekPlanBuilder.SlotState.Upcoming && !s.IsOptional);
            if (slot is not null)
            {
                return new WidgetNextDto(day.Date.DayOfWeek.ToString(), day.Date, slot.Label, slot.Kind.ToString());
            }
        }
        return null;
    }

    // One word per day for the week strip. A day is Missed if anything on it was,
    // Done once everything that counts is done, Rest when nothing counts, and
    // Upcoming otherwise (including a day partly done).
    private static string DayState(WeekPlanBuilder.DayResult day)
    {
        var counting = day.Slots.Where(Counts).ToList();
        if (counting.Count == 0) return "Rest";
        if (counting.Any(s => s.State == WeekPlanBuilder.SlotState.Missed)) return "Missed";
        if (counting.All(s => s.State == WeekPlanBuilder.SlotState.Done)) return "Done";
        return "Upcoming";
    }
}
