using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;
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
    private readonly ICalendarClient? _calendar;

    public PlanController(AppDbContext db, TimeProvider? time = null, ICalendarClient? calendar = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
        _calendar = calendar;
    }

    private static DateOnly MondayOf(DateOnly d) => CalendarDates.MondayOf(d);

    // The training day (3am cutoff), like routines and the frontend: between
    // midnight and 3am the day still in progress is yesterday's, so its slots
    // aren't "missed" yet.
    private DateOnly Today() => CalendarDates.TrainingDay(_time.LocalNow());

    [HttpGet]
    public async Task<ActionResult<WeekPlanDto>> Get([FromQuery] DateOnly? weekStart, CancellationToken ct = default)
    {
        var today = Today();
        var monday = MondayOf(weekStart ?? today);

        var calendarOn = _calendar is { IsConfigured: true };
        string? calendarMessage = null;

        if (calendarOn)
        {
            try
            {
                var fromCalendar = await PlanWeekLoader.LoadFromCalendarAsync(_db, _calendar!, monday, today, ct);
                return Ok(ToDto(fromCalendar));
            }
            catch (CalendarUnavailableException ex)
            {
                // The page still works from the program's own week; it just says so.
                calendarMessage = ex.Message;
            }
        }

        var loaded = await PlanWeekLoader.LoadAsync(_db, monday, today);
        var week = loaded.Week;

        var ankle = new AnkleCircuitDto(PlanWeekLoader.AnkleCircuitRoutineId, loaded.AnkleCompletedDates);

        return Ok(new WeekPlanDto(
            week.WeekStart,
            week.Days.Select(ToDto).ToList(),
            week.Warnings,
            ankle,
            CalendarEnabled: calendarOn,
            CalendarUnavailable: calendarOn,
            CalendarMessage: calendarMessage));
    }

    private static PlanSlotDto ToDto(WeekPlanBuilder.SlotResult s) => new(
        s.SlotId, s.Label, s.Kind.ToString(), s.State.ToString(),
        s.IsOptional, s.IsMoved, s.IsManual, s.TimeOfDay?.ToString(), s.CalendarUid, s.IsLinked);

    private static PlanDayDto ToDto(WeekPlanBuilder.DayResult d) => new(
        d.DayOfWeek, d.Date, DayNames[d.DayOfWeek], d.Slots.Select(ToDto).ToList());

    private static WeekPlanDto ToDto(PlanWeekLoader.CalendarLoaded c) => new(
        c.Week.WeekStart,
        c.Week.Days.Select(ToDto).ToList(),
        c.Week.Warnings,
        new AnkleCircuitDto(PlanWeekLoader.AnkleCircuitRoutineId, c.AnkleCompletedDates),
        CalendarEnabled: true,
        Unplaced: c.Week.Unplaced.Select(ToDto).ToList(),
        OtherEvents: c.OtherEvents.Select(e => new CalendarEventDto(e.Uid, e.Title, e.Day, e.Part?.ToString())).ToList());

    // Moving a slot and ticking it are the same write - both are "this week
    // deviates from the program here" - so they share one row and one endpoint.
    [HttpPut("slots/{slotId}")]
    public async Task<IActionResult> UpdateSlot(int slotId, UpdatePlanSlotRequest request)
    {
        if (!await _db.PlanSlots.AnyAsync(s => s.Id == slotId)) return NotFound();

        if (request.DayOfWeek is int d && (d < 0 || d >= WeekPlanBuilder.DaysInWeek))
        {
            return BadRequest(new { error = $"DayOfWeek must be 0-{WeekPlanBuilder.DaysInWeek - 1}." });
        }

        if (!Enum.TryParse<PlanSlotStatus>(request.Status ?? nameof(PlanSlotStatus.Auto), out var status))
        {
            return BadRequest(new { error = $"Unknown status '{request.Status}'." });
        }

        var start = MondayOf(request.WeekStart);

        var row = await _db.PlanSlotWeeks
            .FirstOrDefaultAsync(w => w.PlanSlotId == slotId && w.WeekStart == start);

        // A slot back on its default day with nothing manual said about it is
        // just the program - drop the row rather than storing a no-op deviation.
        if (request.DayOfWeek is null && status == PlanSlotStatus.Auto && row?.CalendarUid is null)
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

    // Ties a Training-calendar event to a session for one week - for an event whose
    // title the matcher didn't recognise. The calendar still owns the day and part of
    // day; this stores only which event is which session. A null CalendarUid unlinks.
    [HttpPut("slots/{slotId}/link")]
    public async Task<IActionResult> LinkSlot(int slotId, LinkPlanSlotRequest request, CancellationToken ct = default)
    {
        var slot = await _db.PlanSlots.FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot is null) return NotFound();
        if (slot.Kind is not (PlanSlotKind.Gym or PlanSlotKind.Field or PlanSlotKind.Aerobic))
        {
            return BadRequest(new { error = "Only gym, field and aerobic sessions are tied to calendar events." });
        }

        var start = MondayOf(request.WeekStart);
        var uid = string.IsNullOrWhiteSpace(request.CalendarUid) ? null : request.CalendarUid.Trim();

        if (uid is not null)
        {
            if (_calendar is not { IsConfigured: true }) return BadRequest(new { error = "No calendar is set up." });

            IReadOnlyList<CalendarEvent> events;
            try
            {
                events = await _calendar.GetEventsAsync(start, start.AddDays(WeekPlanBuilder.DaysInWeek - 1), ct);
            }
            catch (CalendarUnavailableException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
            if (!events.Any(e => e.Uid == uid && e.Day >= start && e.Day < start.AddDays(WeekPlanBuilder.DaysInWeek)))
            {
                return BadRequest(new { error = "That event isn't in this week of the calendar." });
            }

            // One event stands for one session.
            var others = await _db.PlanSlotWeeks
                .Where(w => w.WeekStart == start && w.PlanSlotId != slotId && w.CalendarUid == uid)
                .ToListAsync();
            foreach (var o in others) o.CalendarUid = null;
        }

        var row = await _db.PlanSlotWeeks.FirstOrDefaultAsync(w => w.PlanSlotId == slotId && w.WeekStart == start);
        if (row is null)
        {
            if (uid is not null)
            {
                _db.PlanSlotWeeks.Add(new PlanSlotWeek
                {
                    PlanSlotId = slotId, WeekStart = start, Status = PlanSlotStatus.Auto, CalendarUid = uid
                });
            }
        }
        else
        {
            row.CalendarUid = uid;
            if (row.DayOfWeek is null && row.Status == PlanSlotStatus.Auto) _db.PlanSlotWeeks.Remove(row);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Puts a session on a day and part of the day by writing to the Training calendar:
    // a new event if the session has none this week, otherwise the same event moved.
    // The calendar stays the only place the day lives; nothing is stored about it here.
    [HttpPut("slots/{slotId}/place")]
    public async Task<IActionResult> PlaceSlot(int slotId, PlacePlanSlotRequest request, CancellationToken ct = default)
    {
        var slot = await _db.PlanSlots.FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot is null) return NotFound();
        if (slot.Kind is not (PlanSlotKind.Gym or PlanSlotKind.Field or PlanSlotKind.Aerobic))
        {
            return BadRequest(new { error = "Only gym, field and aerobic sessions go in the calendar." });
        }
        if (!Enum.TryParse<TimeOfDay>(request.TimeOfDay, out var part) || !Enum.IsDefined(part))
        {
            return BadRequest(new { error = "TimeOfDay must be Morning, Arvo or Evening." });
        }
        var start = MondayOf(request.WeekStart);
        if (request.Day < start || request.Day > start.AddDays(WeekPlanBuilder.DaysInWeek - 1))
        {
            return BadRequest(new { error = "That day isn't in this week." });
        }
        if (_calendar is not { IsConfigured: true }) return BadRequest(new { error = "No calendar is set up." });

        try
        {
            var match = await PlanWeekLoader.MatchAsync(_db, _calendar, start, ct);
            if (match.Matched.BySlot.TryGetValue(slotId, out var existing))
            {
                await _calendar.MoveEventAsync(existing, request.Day, part, ct);
            }
            else
            {
                // Titled with the short name (Gym 1, Field 2) so it is found by title again.
                await _calendar.CreateEventAsync(WeekSoFarBuilder.ShortLabel(slot.Label), request.Day, part, ct);
            }
        }
        catch (CalendarConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (CalendarUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        return NoContent();
    }

    // Takes a session out of the calendar by deleting its event, and forgets any link.
    [HttpDelete("slots/{slotId}/event")]
    public async Task<IActionResult> RemoveSlotEvent(int slotId, [FromQuery] DateOnly weekStart, CancellationToken ct = default)
    {
        if (!await _db.PlanSlots.AnyAsync(s => s.Id == slotId)) return NotFound();
        if (_calendar is not { IsConfigured: true }) return BadRequest(new { error = "No calendar is set up." });

        var start = MondayOf(weekStart);
        try
        {
            var match = await PlanWeekLoader.MatchAsync(_db, _calendar, start, ct);
            if (match.Matched.BySlot.TryGetValue(slotId, out var existing))
            {
                await _calendar.DeleteEventAsync(existing, ct);
            }
        }
        catch (CalendarConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (CalendarUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        var row = await _db.PlanSlotWeeks.FirstOrDefaultAsync(w => w.PlanSlotId == slotId && w.WeekStart == start);
        if (row is not null)
        {
            row.CalendarUid = null;
            if (row.DayOfWeek is null && row.Status == PlanSlotStatus.Auto) _db.PlanSlotWeeks.Remove(row);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
