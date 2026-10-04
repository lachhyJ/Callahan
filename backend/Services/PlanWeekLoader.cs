using Callahan.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// Loads one week of the plan: the program's slots, this week's deviations, and
// what was actually logged, run through WeekPlanBuilder. It lives here rather than
// in PlanController so the Plan page and the home screen widget are fed by the
// same code - a second copy of these queries would drift, and then the widget and
// the page would disagree about whether a session was done.
public static class PlanWeekLoader
{
    // The Daily Ankle Circuit, seeded in AppDbContext. Referenced by Id because
    // it is deliberately not a plan slot - see AnkleCircuitDto.
    public const int AnkleCircuitRoutineId = 1;

    public record Loaded(WeekPlanBuilder.WeekResult Week, List<DateOnly> AnkleCompletedDates);

    public static async Task<Loaded> LoadAsync(AppDbContext db, DateOnly weekStart, DateOnly today)
    {
        var start = CalendarDates.MondayOf(weekStart);
        var end = start.AddDays(WeekPlanBuilder.DaysInWeek - 1);

        var slots = await db.PlanSlots
            .Include(s => s.WorkoutTemplate)
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .Select(s => new WeekPlanBuilder.SlotInput(
                s.Id, s.DefaultDayOfWeek, s.SlotOrder, s.Kind, s.Label, s.IsOptional,
                s.WorkoutTemplateId, s.ActivitySessionTypeId, s.RoutineId,
                s.WorkoutTemplate != null ? s.WorkoutTemplate.LowerBodyLoad : null))
            .ToListAsync();

        var overrides = await db.PlanSlotWeeks
            .Where(w => w.WeekStart == start)
            .Select(w => new WeekPlanBuilder.OverrideInput(w.PlanSlotId, w.DayOfWeek, w.Status))
            .ToListAsync();

        var gym = await db.WorkoutSessions
            .Where(s => s.Date >= start && s.Date <= end && s.WorkoutTemplateId != null)
            .Select(s => new { s.Date, TemplateId = s.WorkoutTemplateId!.Value })
            .ToListAsync();

        var activities = await db.Activities
            .Where(a => a.Date >= start && a.Date <= end && a.ActivitySessionTypeId != null)
            .Select(a => new { a.Date, SessionTypeId = a.ActivitySessionTypeId!.Value })
            .ToListAsync();

        var routines = await db.RoutineCompletions
            .Where(c => c.Date >= start && c.Date <= end)
            .Select(c => new { c.Date, c.RoutineId })
            .ToListAsync();

        var week = WeekPlanBuilder.Build(
            start, today, slots, overrides,
            new WeekPlanBuilder.LoggedInput(
                gym.Select(g => (g.Date, g.TemplateId)).ToList(),
                activities.Select(a => (a.Date, a.SessionTypeId)).ToList(),
                routines.Select(r => (r.Date, r.RoutineId)).ToList()));

        var ankle = routines.Where(r => r.RoutineId == AnkleCircuitRoutineId).Select(r => r.Date).ToList();
        return new Loaded(week, ankle);
    }
}
