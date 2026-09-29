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
    private readonly TimeProvider _time;
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
    //
    // 2: the underlying session dates changed, not the report shape. 31 gym
    // sessions logged in the morning had been attributed to the previous day
    // (a UTC off-by-one, since fixed by trainingDayIso on the client), so every
    // snapshot from Sep 2025 through Jul 2026 was counting them under the wrong
    // month or day. Their stored JSON is stale even though the DTO is unchanged.
    //
    // 3: added the Ultimate section - monthly whole-recording GPS km across
    // Ultimate activities, its per-session-type split, and a count of sessions
    // logged without GPS distance. v2 snapshots have no such field and rebuild.
    //
    // 4: gym volume now excludes warmup sets everywhere. The taper section's
    // actual-reduction was computed from all sets, so v3 snapshots carry the
    // old figure and rebuild.
    internal const int CurrentReportSchemaVersion = 4;

    public MonthlyReportsController(AppDbContext db, MonthlyReportBuilder builder, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
        _builder = builder;
    }

    [HttpGet]
    public async Task<ActionResult<List<MonthlyReportListEntryDto>>> List()
    {
        var today = _time.Today();
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

        var today = _time.Today();
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
        // real view, so store a row carrying ViewedAt. Its ReportJson is only
        // the month so far: GetOrComputeAsync rebuilds any snapshot computed
        // before the lock date, so it never becomes the locked report.
        var dto = await _builder.BuildAsync(year, month);
        row = new MonthlyReport
        {
            Year = year,
            Month = month,
            ReportJson = Serialize(dto),
            ComputedAt = _time.GetUtcNow().UtcDateTime,
            SchemaVersion = CurrentReportSchemaVersion,
            ViewedAt = DateTime.UtcNow,
        };
        _db.MonthlyReports.Add(row);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race to insert this month's row — (Year, Month) is
            // unique. The frontend fires this from an effect that React runs
            // twice in development, so two concurrent calls both read "no row"
            // and both insert. Whoever won created the row; marking it viewed
            // is all this endpoint wanted anyway.
            _db.Entry(row).State = EntityState.Detached;
            var winner = await _db.MonthlyReports.FirstOrDefaultAsync(r => r.Year == year && r.Month == month);
            if (winner is null) throw;
            winner.ViewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

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
        var lockDate = followingMonthStart.AddDays(LockDayOfFollowingMonth - 1);
        var shouldBeLocked = today >= lockDate;

        var existing = await _db.MonthlyReports.FirstOrDefaultAsync(r => r.Year == year && r.Month == month);

        // A row can exist before the lock day - MarkViewed stores one when a
        // report is first opened mid-month - so the lock is only final if the
        // snapshot was computed on or after the lock date. An earlier one holds
        // part of the month and is rebuilt here like an old-schema row.
        var snapshotIsFinal = existing is not null
            && existing.SchemaVersion >= CurrentReportSchemaVersion
            && DateOnly.FromDateTime(existing.ComputedAt.ToLocalTime()) >= lockDate;

        if (shouldBeLocked && snapshotIsFinal && existing is not null)
        {
            // Already snapshotted at the current shape and past the lock
            // point — immutable, return as-is.
            var locked = Deserialize(existing.ReportJson) with { IsLocked = true, IsProvisional = false, ViewedAt = existing.ViewedAt };
            return locked;
        }

        if (shouldBeLocked)
        {
            // Past the lock point with no snapshot, one written under an older
            // report shape, or one computed before the month locked — compute
            // and store. Rebuilding overwrites
            // the existing row rather than replacing it, so ViewedAt survives.
            var toSnapshot = await _builder.BuildAsync(year, month);
            toSnapshot = toSnapshot with { IsLocked = true, IsProvisional = false, ViewedAt = existing?.ViewedAt };

            var isNewRow = existing is null;
            if (existing is null)
            {
                existing = new MonthlyReport { Year = year, Month = month };
                _db.MonthlyReports.Add(existing);
            }
            existing.ReportJson = Serialize(toSnapshot);
            existing.ComputedAt = _time.GetUtcNow().UtcDateTime;
            existing.SchemaVersion = CurrentReportSchemaVersion;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (isNewRow)
            {
                // Lost the race to create this month's row — (Year, Month) is
                // unique, and concurrent readers both see "no snapshot" and
                // both insert. Two things make that routine rather than
                // exotic: the frontend's effects run twice in development,
                // and the first load after a schema-version bump has every
                // month rebuilding at once. The winner computed from the same
                // data, so take their row rather than failing the request.
                _db.Entry(existing).State = EntityState.Detached;
                var winner = await _db.MonthlyReports.FirstOrDefaultAsync(r => r.Year == year && r.Month == month);
                if (winner is null) throw;
                return Deserialize(winner.ReportJson) with
                {
                    IsLocked = true,
                    IsProvisional = false,
                    ViewedAt = winner.ViewedAt,
                };
            }

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
