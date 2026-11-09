using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

public record MilestoneDto(string Title, DateOnly Day);

[ApiController]
[Authorize]
[Route("api/milestones")]
public class MilestonesController : ControllerBase
{
    private readonly TimeProvider _time;
    private readonly ICalendarClient? _calendar;

    public MilestonesController(TimeProvider? time = null, ICalendarClient? calendar = null)
    {
        _time = time ?? TimeProvider.System;
        _calendar = calendar;
    }

    // The next review or test day in the Training calendar, or 204 when there is none
    // in the lookahead. A calendar that isn't set up or can't be reached is also a 204:
    // this is a one-line hint on the Dashboard, never a reason to show an error.
    [HttpGet("next")]
    public async Task<ActionResult<MilestoneDto?>> Next(CancellationToken ct = default)
    {
        if (_calendar is not { IsConfigured: true }) return NoContent();

        var today = CalendarDates.TrainingDay(_time.LocalNow());
        IReadOnlyList<CalendarEvent> events;
        try
        {
            events = await _calendar.GetEventsAsync(today, today.AddDays(ProgramMilestones.LookaheadDays), ct);
        }
        catch (CalendarUnavailableException)
        {
            return NoContent();
        }

        var next = ProgramMilestones.Next(events, today);
        return next is null ? NoContent() : Ok(new MilestoneDto(next.Title, next.Day));
    }
}
