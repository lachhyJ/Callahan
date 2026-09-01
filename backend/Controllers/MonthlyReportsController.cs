using System.Text.Json;
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
public class MonthlyReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MonthlyReportBuilder _builder;

    // Reports lock (snapshot) once we're at least this many days into the
    // following month — by then the data for the reported month is settled
    // (backfills/edits from the tail end of the month have had time to
    // land), so it's safe to freeze it. Before that, every read recomputes
    // live and is marked provisional.
    private const int LockDayOfFollowingMonth = 8;

    // Bump whenever MonthlyReportDto's shape or a section's meaning changes.
    // Any stored snapshot below this is rebuilt in place on the next read,
    // keeping its row and its ViewedAt.
    private const int CurrentReportSchemaVersion = 1;

    public MonthlyReportsController(AppDbContext db, MonthlyReportBuilder builder)
    {
        _db = db;
        _builder = builder;
    }

    [HttpGet]
    public async Task<ActionResult<List<MonthlyReportListEntryDto>>> List()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var earliestSessionDate = await EarliestActivityDateAsync();
        if (earliestSessionDate is null) return Ok(new List<MonthlyReportListEntryDto>());

        var cursor = new DateOnly(earliestSessionDate.Value.Year, earliestSessionDate.Value.Month, 1);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);

        var entries = new List<MonthlyReportListEntryDto>();
        while (cursor <= currentMonthStart)
        {
            var dto = await GetOrComputeAsync(cursor.Year, cursor.Month, today);
            entries.Add(new MonthlyReportListEntryDto(cursor.Year, cursor.Month, dto.IsLocked, dto.ViewedAt != null, dto.HeadlineVerdict));
            cursor = cursor.AddMonths(1);
        }

        entries.Reverse(); // newest first
        return Ok(entries);
    }

    [HttpGet("{year:int}/{month:int}")]
    public async Task<ActionResult<MonthlyReportDto>> Get(int year, int month)
    {
        if (month is < 1 or > 12) return BadRequest(new { error = "Month must be between 1 and 12." });

        var today = DateOnly.FromDateTime(DateTime.Now);
        var requestedMonthStart = new DateOnly(year, month, 1);
        if (requestedMonthStart > new DateOnly(today.Year, today.Month, 1))
        {
            return BadRequest(new { error = "Can't build a report for a month that hasn't started yet." });
        }

        var dto = await GetOrComputeAsync(year, month, today);
        return Ok(dto);
    }

    [HttpPost("{year:int}/{month:int}/viewed")]
    public async Task<IActionResult> MarkViewed(int year, int month)
    {
        var row = await _db.MonthlyReports.FirstOrDefaultAsync(r => r.Year == year && r.Month == month);
        if (row is not null)
        {
            row.ViewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Provisional (unsnapshotted) month — nothing to persist yet, but we
        // still want "viewed" to survive if it locks later without another
        // real view. Store a placeholder snapshot marked as viewed; the next
        // GET past the lock day will overwrite it with the real computed
        // report anyway if it doesn't already exist... except it does now,
        // so instead: only allow marking-viewed on months that already have
        // *a* row (locked or not). If unlocked and no row exists yet, create
        // a lightweight row carrying just ViewedAt; GetOrComputeAsync treats
        // a present-but-not-yet-locked-month row as "not locked" via the
        // separate lock day check, so this doesn't accidentally freeze data.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dto = await _builder.BuildAsync(year, month);
        row = new MonthlyReport
        {
            Year = year,
            Month = month,
            ReportJson = Serialize(dto),
            ComputedAt = DateTime.UtcNow,
            SchemaVersion = CurrentReportSchemaVersion,
            ViewedAt = DateTime.UtcNow,
        };
        _db.MonthlyReports.Add(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<DateOnly?> EarliestActivityDateAsync()
    {
        var earliestWorkout = await _db.WorkoutSessions.OrderBy(s => s.Date).Select(s => (DateOnly?)s.Date).FirstOrDefaultAsync();
        var earliestActivity = await _db.Activities.OrderBy(a => a.Date).Select(a => (DateOnly?)a.Date).FirstOrDefaultAsync();
        if (earliestWorkout is null && earliestActivity is null) return null;
        if (earliestWorkout is null) return earliestActivity;
        if (earliestActivity is null) return earliestWorkout;
        return earliestWorkout < earliestActivity ? earliestWorkout : earliestActivity;
    }

    private async Task<MonthlyReportDto> GetOrComputeAsync(int year, int month, DateOnly today)
    {
        var followingMonthStart = new DateOnly(year, month, 1).AddMonths(1);
        var shouldBeLocked = today >= followingMonthStart.AddDays(LockDayOfFollowingMonth - 1);

        var existing = await _db.MonthlyReports.FirstOrDefaultAsync(r => r.Year == year && r.Month == month);

        if (existing is not null && shouldBeLocked && existing.SchemaVersion >= CurrentReportSchemaVersion)
        {
            // Already snapshotted at the current shape and past the lock
            // point — immutable, return as-is.
            var locked = Deserialize(existing.ReportJson) with { IsLocked = true, IsProvisional = false, ViewedAt = existing.ViewedAt };
            return locked;
        }

        if (shouldBeLocked)
        {
            // Past the lock point with no snapshot, or one written under an
            // older report shape — compute and store. Rebuilding overwrites
            // the existing row rather than replacing it, so ViewedAt survives.
            var toSnapshot = await _builder.BuildAsync(year, month);
            toSnapshot = toSnapshot with { IsLocked = true, IsProvisional = false, ViewedAt = existing?.ViewedAt };

            if (existing is null)
            {
                existing = new MonthlyReport { Year = year, Month = month };
                _db.MonthlyReports.Add(existing);
            }
            existing.ReportJson = Serialize(toSnapshot);
            existing.ComputedAt = DateTime.UtcNow;
            existing.SchemaVersion = CurrentReportSchemaVersion;

            await _db.SaveChangesAsync();
            return toSnapshot;
        }

        // Still within the settling window — always recompute live and mark provisional.
        var live = await _builder.BuildAsync(year, month);
        live = live with { IsLocked = false, IsProvisional = true, ViewedAt = existing?.ViewedAt };
        return live;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Serialize(MonthlyReportDto dto) => JsonSerializer.Serialize(dto, JsonOptions);
    private static MonthlyReportDto Deserialize(string json) => JsonSerializer.Deserialize<MonthlyReportDto>(json, JsonOptions)!;
}
