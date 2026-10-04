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
    private static readonly string[] DayNames =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public PlanController(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private static DateOnly MondayOf(DateOnly d) => CalendarDates.MondayOf(d);

    // The training day (3am cutoff), like routines and the frontend: between
    // midnight and 3am the day still in progress is yesterday's, so its slots
    // aren't "missed" yet.
    private DateOnly Today() => CalendarDates.TrainingDay(_time.LocalNow());

    [HttpGet]
    public async Task<ActionResult<WeekPlanDto>> Get([FromQuery] DateOnly? weekStart)
    {
        var today = Today();
        var loaded = await PlanWeekLoader.LoadAsync(_db, MondayOf(weekStart ?? today), today);
        var week = loaded.Week;

        var ankle = new AnkleCircuitDto(PlanWeekLoader.AnkleCircuitRoutineId, loaded.AnkleCompletedDates);

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
