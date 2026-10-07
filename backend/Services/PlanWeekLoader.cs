using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services.Calendar;
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

        var slots = await SlotsAsync(db);

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

    private static Task<List<WeekPlanBuilder.SlotInput>> SlotsAsync(AppDbContext db) => db.PlanSlots
        .Include(s => s.WorkoutTemplate)
        .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
        .Select(s => new WeekPlanBuilder.SlotInput(
            s.Id, s.DefaultDayOfWeek, s.SlotOrder, s.Kind, s.Label, s.IsOptional,
            s.WorkoutTemplateId, s.ActivitySessionTypeId, s.RoutineId,
            s.WorkoutTemplate != null ? s.WorkoutTemplate.LowerBodyLoad : null))
        .ToListAsync();

    public record CalendarLoaded(
        WeekPlanBuilder.CalendarWeekResult Week, List<CalendarEvent> OtherEvents, List<DateOnly> AnkleCompletedDates);

    // The week as the Training calendar has it. Reads the calendar first, so a
    // calendar that can't be reached throws CalendarUnavailableException before
    // anything else is queried and the caller can fall back to the program's week.
    public static async Task<CalendarLoaded> LoadFromCalendarAsync(
        AppDbContext db, ICalendarClient calendar, DateOnly weekStart, DateOnly today, CancellationToken ct)
    {
        var start = CalendarDates.MondayOf(weekStart);
        var end = start.AddDays(WeekPlanBuilder.DaysInWeek - 1);

        var events = (await calendar.GetEventsAsync(start, end, ct))
            .Where(e => e.Day >= start && e.Day <= end)
            .ToList();

        var slots = await SlotsAsync(db);

        var weekRows = await db.PlanSlotWeeks.Where(w => w.WeekStart == start).ToListAsync();
        var overrides = weekRows
            .Select(w => new WeekPlanBuilder.OverrideInput(w.PlanSlotId, w.DayOfWeek, w.Status))
            .ToList();
        var linked = weekRows.Where(w => w.CalendarUid != null).ToDictionary(w => w.PlanSlotId, w => w.CalendarUid!);

        var sessions = slots
            .Where(s => s.Kind is PlanSlotKind.Gym or PlanSlotKind.Field or PlanSlotKind.Aerobic)
            .Select(s => new CalendarMatcher.SlotKey(s.SlotId, s.Kind, s.Label, linked.GetValueOrDefault(s.SlotId)))
            .ToList();
        var matched = CalendarMatcher.Match(sessions, events);

        var placements = matched.BySlot.ToDictionary(
            kv => kv.Key,
            kv => new WeekPlanBuilder.Placement(kv.Value.Day, kv.Value.Part, kv.Value.Uid, matched.Linked.Contains(kv.Key)));

        // Done is week-level, by the same rule as the widget: the non-optional gym and
        // field slots, in program order, each fillable by a log from any day this week.
        var countable = slots.Where(s => !s.IsOptional && s.Kind is PlanSlotKind.Gym or PlanSlotKind.Field).ToList();
        var gymSlots = countable.Where(s => s.Kind == PlanSlotKind.Gym).ToList();
        var fieldSlots = countable.Where(s => s.Kind == PlanSlotKind.Field).ToList();
        var (gym, activities) = await WeekSoFarLoader.LoadLogsAsync(db, start, end);
        WeekSoFarBuilder.SlotInput Fill(WeekPlanBuilder.SlotInput s) =>
            new(s.Kind, s.Label, s.WorkoutTemplateId, s.ActivitySessionTypeId);
        var fill = WeekSoFarBuilder.Fill(gymSlots.Select(Fill).ToList(), fieldSlots.Select(Fill).ToList(), gym, activities);
        var filled = new HashSet<int>();
        for (var i = 0; i < gymSlots.Count; i++) if (fill.GymFilled[i]) filled.Add(gymSlots[i].SlotId);
        for (var i = 0; i < fieldSlots.Count; i++) if (fill.FieldFilled[i]) filled.Add(fieldSlots[i].SlotId);

        var week = WeekPlanBuilder.BuildFromCalendar(start, today, slots, overrides, filled, placements);

        var ankle = await db.RoutineCompletions
            .Where(c => c.Date >= start && c.Date <= end && c.RoutineId == AnkleCircuitRoutineId)
            .Select(c => c.Date)
            .ToListAsync();

        return new CalendarLoaded(week, matched.Other, ankle);
    }
}
