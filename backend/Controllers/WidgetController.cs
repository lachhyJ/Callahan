using System.Security.Cryptography;
using System.Text;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// Feeds the iOS home screen widget. The widget is its own process and free
// provisioning gives it no App Group, so it can't borrow the app's login JWT;
// it carries this one narrow key instead. The key authenticates exactly this
// route and nothing else - it is not a bearer token and the auth middleware
// never sees it, which is why the route opts out of the fallback policy and
// checks the header itself.
//
// Every failure is a 404, including "no key configured", so the route is
// indistinguishable from one that doesn't exist (same posture as dev-login).
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("widget")]
[Route("api/widget")]
public class WidgetController : ControllerBase
{
    public const string KeyHeader = "X-Widget-Key";

    // A status older than this is better shown (the widget flags it as stale)
    // than hidden, but past two weeks it says nothing about now.
    private const int WindowDays = 14;

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    public WidgetController(AppDbContext db, IConfiguration config, TimeProvider? time = null)
    {
        _db = db;
        _config = config;
        _time = time ?? TimeProvider.System;
    }

    // Newest day that has a status, not "latest readable" like /wellness/latest:
    // that one keys off sleep/HRV and would skip a status-only row.
    [HttpGet("training-status")]
    public async Task<ActionResult<WidgetTrainingStatusDto>> GetTrainingStatus()
    {
        if (!KeyMatches()) return NotFound();

        var cutoff = _time.Today().AddDays(-WindowDays);
        var row = await _db.DailyWellness
            .Where(w => w.Date >= cutoff && w.TrainingStatusCode != null)
            .OrderByDescending(w => w.Date)
            .Select(w => new WidgetTrainingStatusDto(
                w.Date, w.TrainingStatusCode!.Value, w.AcwrRatio, w.AcuteLoad, w.ChronicLoad))
            .FirstOrDefaultAsync();

        if (row is null) return NoContent();
        return Ok(row);
    }

    // Built from the same loader as the Plan page, so the widget cannot disagree
    // with it about what is done. "Today" is the training day (3 am cutoff, in the
    // user-switchable zone), sent back as TrainingDay so the widget never works it
    // out from the device clock.
    [HttpGet("today")]
    public async Task<ActionResult<WidgetTodayDto>> GetToday()
    {
        if (!KeyMatches()) return NotFound();

        var today = CalendarDates.TrainingDay(_time.LocalNow());
        var weekStart = CalendarDates.MondayOf(today);

        var week = (await PlanWeekLoader.LoadAsync(_db, weekStart, today)).Week;
        var dto = WidgetTodayBuilder.Build(week, null, today);

        // Nothing left this week (Sunday evening): look at next week so "next" is
        // not empty, but only then - it is a second round of queries.
        if (dto.Next is null)
        {
            var nextWeek = (await PlanWeekLoader.LoadAsync(_db, weekStart.AddDays(WeekPlanBuilder.DaysInWeek), today)).Week;
            dto = WidgetTodayBuilder.Build(week, nextWeek, today);
        }

        return Ok(dto);
    }

    private bool KeyMatches()
    {
        var configured = _config["Widget:Key"];
        if (string.IsNullOrEmpty(configured)) return false;

        var supplied = Request.Headers[KeyHeader].ToString();
        if (supplied.Length == 0) return false;

        // FixedTimeEquals needs equal lengths; hashing first makes the length of
        // the real key irrelevant and keeps the comparison constant-time.
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
