using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Controllers;

// The list behind the lock-screen quotes widget. The widget itself reads it through
// WidgetController (its own key); this is the app's side, behind the normal login.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    // Only there to stop a paste accident (a paragraph landing as one quote). Whether
    // a quote fits the lock-screen slot is the page's soft warning, not a rule here.
    public const int MaxLength = 200;

    private const int RecoveryWindowDays = 7;

    private readonly AppDbContext _db;

    public QuotesController(AppDbContext db)
    {
        _db = db;
    }

    // Same lazy purge as sessions and activities: on a read path, no schedule.
    private async Task PurgeExpiredAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RecoveryWindowDays);
        var expired = await _db.Quotes.IgnoreQueryFilters()
            .Where(q => q.DeletedAt != null && q.DeletedAt < cutoff)
            .ToListAsync();
        if (expired.Count == 0) return;

        _db.Quotes.RemoveRange(expired);
        await _db.SaveChangesAsync();
    }

    private static QuoteDto ToDto(Quote q) => new(q.Id, q.Text, q.CreatedAt);

    [HttpGet]
    public async Task<ActionResult<List<QuoteDto>>> GetAll()
    {
        await PurgeExpiredAsync();

        var quotes = await _db.Quotes
            .OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.Id)
            .ToListAsync();
        return Ok(quotes.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AddQuotesResultDto>> Add(AddQuotesRequest request)
    {
        var texts = request.Texts;
        if (texts is null || texts.Count == 0) return BadRequest("No quotes given.");

        // Small list, so compare in memory: SQLite's NOCASE only folds ASCII, and the
        // live rows plus the recoverable ones are needed together anyway.
        var existing = await _db.Quotes.IgnoreQueryFilters().ToListAsync();
        var byText = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in existing.OrderBy(q => q.DeletedAt != null)) byText.TryAdd(q.Text, q);

        var result = new List<Quote>();
        var skipped = new List<SkippedQuoteDto>();
        var now = DateTime.UtcNow;

        foreach (var raw in texts)
        {
            var text = (raw ?? "").Trim();
            if (text.Length == 0) { skipped.Add(new("", "blank")); continue; }
            if (text.Length > MaxLength) { skipped.Add(new(text, "too long")); continue; }

            if (byText.TryGetValue(text, out var found))
            {
                if (found.DeletedAt is null) { skipped.Add(new(text, "duplicate")); continue; }

                // Typing a quote that is waiting out its recovery window brings it back
                // rather than leaving two rows.
                found.DeletedAt = null;
                result.Add(found);
                continue;
            }

            var quote = new Quote { Text = text, CreatedAt = now };
            _db.Quotes.Add(quote);
            byText[text] = quote;
            result.Add(quote);
        }

        await _db.SaveChangesAsync();
        return Ok(new AddQuotesResultDto(result.Select(ToDto).ToList(), skipped));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();

        quote.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<QuoteDto>> Restore(int id)
    {
        var quote = await _db.Quotes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == id && q.DeletedAt != null);
        if (quote is null) return NotFound();

        quote.DeletedAt = null;
        await _db.SaveChangesAsync();
        return Ok(ToDto(quote));
    }
}
