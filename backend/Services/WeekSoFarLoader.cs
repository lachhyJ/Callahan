using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// The queries behind the "this week" widget. Soft-deleted sessions and activities
// are already excluded by the DbContext's query filters.
public static class WeekSoFarLoader
{
    public static async Task<WidgetWeekDto> LoadAsync(AppDbContext db, DateOnly trainingDay)
    {
        var weekStart = CalendarDates.MondayOf(trainingDay);
        var weekEnd = weekStart.AddDays(WeekPlanBuilder.DaysInWeek - 1);

        // Non-optional Gym and Field slots only, in program order.
        var slots = await db.PlanSlots
            .Where(s => !s.IsOptional && (s.Kind == PlanSlotKind.Gym || s.Kind == PlanSlotKind.Field))
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .Select(s => new WeekSoFarBuilder.SlotInput(s.Kind, s.Label, s.WorkoutTemplateId, s.ActivitySessionTypeId))
            .ToListAsync();

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

        return WeekSoFarBuilder.Build(weekStart, trainingDay, slots, gym, activities);
    }
}
