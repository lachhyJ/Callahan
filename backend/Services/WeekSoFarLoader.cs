using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// The queries behind the "this week" widget. Soft-deleted sessions and activities
// are already excluded by the DbContext's query filters.
public static class WeekSoFarLoader
{
    // With a calendar, the sessions still to do are also placed on the day the Training
    // calendar has them. A calendar that can't be read just leaves them off: the widget
    // is then what it was before the calendar existed.
    public static async Task<WidgetWeekDto> LoadAsync(
        AppDbContext db, DateOnly trainingDay, ICalendarClient? calendar = null, CancellationToken ct = default)
    {
        var weekStart = CalendarDates.MondayOf(trainingDay);
        var weekEnd = weekStart.AddDays(WeekPlanBuilder.DaysInWeek - 1);

        // Non-optional Gym and Field slots of the current block only, in program order.
        var mode = await TrainingModeStore.GetAsync(db);
        var rows = await db.PlanSlots
            .Where(s => (s.Block == TemplateBlock.Any || s.Block == mode) && !s.IsOptional && (s.Kind == PlanSlotKind.Gym || s.Kind == PlanSlotKind.Field))
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .Select(s => new { s.Id, Slot = new WeekSoFarBuilder.SlotInput(s.Kind, s.Label, s.WorkoutTemplateId, s.ActivitySessionTypeId) })
            .ToListAsync();
        var slots = rows.Select(r => r.Slot).ToList();

        var (gym, activities) = await LoadLogsAsync(db, weekStart, weekEnd);

        Dictionary<int, DateOnly>? planned = null;
        if (calendar is { IsConfigured: true })
        {
            try
            {
                var matched = (await PlanWeekLoader.MatchAsync(db, calendar, weekStart, ct)).Matched;
                planned = rows
                    .Select((r, i) => (i, r.Id))
                    .Where(x => matched.BySlot.ContainsKey(x.Id))
                    .ToDictionary(x => x.i, x => matched.BySlot[x.Id].Day);
            }
            catch (CalendarUnavailableException)
            {
                planned = null;
            }
        }

        return WeekSoFarBuilder.Build(weekStart, trainingDay, slots, gym, activities, planned);
    }

    // The due-badge inputs: the current block's non-optional Gym slots with their planned
    // day, then the week's gym logs. Kept apart from LoadAsync, which has no use for the day.
    public static async Task<IReadOnlySet<int>> LoadDueTemplateIdsAsync(AppDbContext db, TemplateBlock mode, DateOnly trainingDay)
    {
        var weekStart = CalendarDates.MondayOf(trainingDay);
        var slots = await db.PlanSlots
            .Where(s => (s.Block == TemplateBlock.Any || s.Block == mode) && !s.IsOptional && s.Kind == PlanSlotKind.Gym)
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .Select(s => new WeekSoFarBuilder.DatedGymSlot(s.WorkoutTemplateId, s.DefaultDayOfWeek))
            .ToListAsync();
        if (slots.Count == 0) return new HashSet<int>();

        var gym = await db.WorkoutSessions
            .Where(s => s.Date >= weekStart && s.Date <= weekStart.AddDays(WeekPlanBuilder.DaysInWeek - 1))
            .Select(s => new WeekSoFarBuilder.GymLog(
                s.Date, s.WorkoutTemplateId, s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null))
            .ToListAsync();

        return WeekSoFarBuilder.DueTemplateIds(slots, gym, weekStart, trainingDay);
    }

    // What was logged in the week, in the shape WeekSoFarBuilder reads. Also the
    // Plan page's source for whether a session was done.
    public static async Task<(List<WeekSoFarBuilder.GymLog> Gym, List<WeekSoFarBuilder.ActivityLog> Activities)> LoadLogsAsync(
        AppDbContext db, DateOnly weekStart, DateOnly weekEnd)
    {
        var gym = await db.WorkoutSessions
            .Where(s => s.Date >= weekStart && s.Date <= weekEnd)
            .OrderBy(s => s.Date).ThenBy(s => s.Id)
            .Select(s => new WeekSoFarBuilder.GymLog(
                s.Date, s.WorkoutTemplateId, s.WorkoutTemplate != null ? s.WorkoutTemplate.Name : null))
            .ToListAsync();

        var activities = await db.Activities
            .Where(a => a.Date >= weekStart && a.Date <= weekEnd)
            .OrderBy(a => a.Date).ThenBy(a => a.Id)
            .Select(a => new WeekSoFarBuilder.ActivityLog(
                a.Date,
                a.Type,
                a.ActivitySessionTypeId,
                a.ActivitySessionType != null ? a.ActivitySessionType.Name : null,
                a.ActivitySessionType != null ? a.ActivitySessionType.Family : (SessionTypeFamily?)null))
            .ToListAsync();

        return (gym, activities);
    }
}
