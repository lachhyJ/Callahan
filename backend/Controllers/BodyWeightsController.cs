using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// Body weight as Garmin Connect has it. Written only by the garmin-sync sidecar; read
// by nothing in the app yet (see BodyWeight).
[ApiController]
[Authorize]
[Route("api/body-weights")]
public class BodyWeightsController : ControllerBase
{
    // Anything outside this is a unit slip (grams sent as kilograms, a typo) rather than
    // a reading, and would poison a trend line later. Skipped, not stored.
    public const double MinKg = 20;
    public const double MaxKg = 300;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public BodyWeightsController(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    [HttpGet]
    public async Task<ActionResult<List<BodyWeightDto>>> GetAll(DateOnly? start = null, DateOnly? end = null)
    {
        var query = _db.BodyWeights.AsQueryable();
        if (start is not null) query = query.Where(w => w.Date >= start);
        if (end is not null) query = query.Where(w => w.Date <= end);

        return Ok(await query
            .OrderBy(w => w.Date)
            .Select(w => new BodyWeightDto(w.Date, w.WeightKg, w.Source))
            .ToListAsync());
    }

    // Upsert by date. Unlike the wellness PUT this only touches the dates it is sent, so
    // a short sync window never clears older history.
    [HttpPut]
    public async Task<ActionResult<UpsertBodyWeightsResultDto>> Upsert(UpsertBodyWeightsRequest request)
    {
        var entries = request.Entries ?? [];
        var today = _time.Today();

        int created = 0, updated = 0, skipped = 0;
        var byDate = entries
            .Where(e => e.Date <= today && double.IsFinite(e.WeightKg) && e.WeightKg is >= MinKg and <= MaxKg)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.Last());
        skipped = entries.Count - byDate.Count;

        var dates = byDate.Keys.ToList();
        var existing = await _db.BodyWeights.Where(w => dates.Contains(w.Date)).ToDictionaryAsync(w => w.Date);

        foreach (var (date, entry) in byDate)
        {
            if (existing.TryGetValue(date, out var row))
            {
                if (row.WeightKg == entry.WeightKg && row.Source == entry.Source) continue;
                row.WeightKg = entry.WeightKg;
                row.Source = entry.Source;
                row.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _db.BodyWeights.Add(new BodyWeight
                {
                    Date = date,
                    WeightKg = entry.WeightKg,
                    Source = entry.Source,
                    CreatedAt = DateTime.UtcNow,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new UpsertBodyWeightsResultDto(created, updated, skipped));
    }
}
