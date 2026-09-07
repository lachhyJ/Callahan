using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PlanController : ControllerBase
{
    // The Daily Ankle Circuit, seeded in AppDbContext. Referenced by Id because
    // it is deliberately not a plan slot - see AnkleCircuitDto.
    private const int AnkleCircuitRoutineId = 1;

    private static readonly string[] DayNames =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private readonly AppDbContext _db;

    public PlanController(AppDbContext db)
    {
        _db = db;
    }

    // Monday-first, matching dateUtils.startOfWeek and the Calendar grid.
    private static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    [HttpGet]
    public async Task<ActionResult<WeekPlanDto>> Get([FromQuery] DateOnly? weekStart)
    {
        var start = MondayOf(weekStart ?? Today());
        var end = start.AddDays(WeekPlanBuilder.DaysInWeek - 1);

        var slots = await _db.PlanSlots
            .Include(s => s.WorkoutTemplate)
            .OrderBy(s => s.DefaultDayOfWeek).ThenBy(s => s.SlotOrder)
            .Select(s => new WeekPlanBuilder.SlotInput(
                s.Id, s.DefaultDayOfWeek, s.SlotOrder, s.Kind, s.Label, s.IsOptional,
                s.WorkoutTemplateId, s.ActivitySessionTypeId, s.RoutineId,
                s.WorkoutTemplate != null ? s.WorkoutTemplate.LowerBodyLoad : null))
            .ToListAsync();

        var overrides = await _db.PlanSlotWeeks
            .Where(w => w.WeekStart == start)
            .Select(w => new WeekPlanBuilder.OverrideInput(w.PlanSlotId, w.DayOfWeek, w.Status))
            .ToListAsync();

        var gym = await _db.WorkoutSessions
            .Where(s => s.Date >= start && s.Date <= end && s.WorkoutTemplateId != null)
            .Select(s => new { s.Date, TemplateId = s.WorkoutTemplateId!.Value })
            .ToListAsync();

        var activities = await _db.Activities
            .Where(a => a.Date >= start && a.Date <= end && a.ActivitySessionTypeId != null)
            .Select(a => new { a.Date, SessionTypeId = a.ActivitySessionTypeId!.Value })
            .ToListAsync();

        var routines = await _db.RoutineCompletions
            .Where(c => c.Date >= start && c.Date <= end)
            .Select(c => new { c.Date, c.RoutineId })
            .ToListAsync();

        var week = WeekPlanBuilder.Build(
            start, Today(), slots, overrides,
            new WeekPlanBuilder.LoggedInput(
                gym.Select(g => (g.Date, g.TemplateId)).ToList(),
                activities.Select(a => (a.Date, a.SessionTypeId)).ToList(),
                routines.Select(r => (r.Date, r.RoutineId)).ToList()));

        var ankle = new AnkleCircuitDto(
            AnkleCircuitRoutineId,
            routines.Where(r => r.RoutineId == AnkleCircuitRoutineId).Select(r => r.Date).ToList());

        return Ok(new WeekPlanDto(
            week.WeekStart,
            week.Days.Select(d => new PlanDayDto(
                d.DayOfWeek, d.Date, DayNames[d.DayOfWeek],
                d.Slots.Select(s => new PlanSlotDto(
                    s.SlotId, s.Label, s.Kind.ToString(), s.State.ToString(),
                    s.IsOptional, s.IsMoved, s.IsManual)).ToList())).ToList(),
            week.Warnings,
            ankle));
    }

    // Moving a slot and ticking it are the same write - both are "this week
    // deviates from the program here" - so they share one row and one endpoint.
    [HttpPut("slots/{slotId}")]
    public async Task<IActionResult> UpdateSlot(int slotId, UpdatePlanSlotRequest request)
    {
        if (!await _db.PlanSlots.AnyAsync(s => s.Id == slotId)) return NotFound();

        if (request.DayOfWeek is int d && (d < 0 || d >= WeekPlanBuilder.DaysInWeek))
        {
            return BadRequest($"DayOfWeek must be 0-{WeekPlanBuilder.DaysInWeek - 1}.");
        }

        if (!Enum.TryParse<PlanSlotStatus>(request.Status ?? nameof(PlanSlotStatus.Auto), out var status))
        {
            return BadRequest($"Unknown status '{request.Status}'.");
        }

        var start = MondayOf(request.WeekStart);

        var row = await _db.PlanSlotWeeks
            .FirstOrDefaultAsync(w => w.PlanSlotId == slotId && w.WeekStart == start);

        // A slot back on its default day with nothing manual said about it is
        // just the program - drop the row rather than storing a no-op deviation.
        if (request.DayOfWeek is null && status == PlanSlotStatus.Auto)
        {
            if (row is not null) _db.PlanSlotWeeks.Remove(row);
        }
        else if (row is null)
        {
            _db.PlanSlotWeeks.Add(new PlanSlotWeek
            {
                PlanSlotId = slotId,
                WeekStart = start,
                DayOfWeek = request.DayOfWeek,
                Status = status
            });
        }
        else
        {
            row.DayOfWeek = request.DayOfWeek;
            row.Status = status;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
