using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

public class TaperConsultService
{
    private const string Model = "claude-sonnet-5";

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TaperConsultService> _logger;

    public TaperConsultService(AppDbContext db, HttpClient http, IConfiguration config, ILogger<TaperConsultService> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<(string Answer, bool ComparedToPriorTaper)> AskAsync(Tournament taperEvent, string question)
    {
        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TaperConsultUnavailableException("AI consult isn't configured yet.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var (systemPrompt, comparedToPriorTaper) = await BuildSystemPromptAsync(taperEvent, today);

        var requestBody = new
        {
            model = Model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = question } },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Anthropic API call failed for taper {TaperEventId}", taperEvent.Id);
            throw new TaperConsultUnavailableException("AI consult is temporarily unavailable — try again shortly.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Anthropic API returned {Status} for taper {TaperEventId}: {Body}", response.StatusCode, taperEvent.Id, body);

            // 401/403 means the key itself is wrong/revoked — a config problem
            // that retrying won't fix, distinct from a genuinely transient
            // failure (429/5xx/network), so the message shouldn't tell the
            // athlete to just try again.
            var message = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                ? "AI consult isn't configured correctly — check the Anthropic API key."
                : "AI consult is temporarily unavailable — try again shortly.";
            throw new TaperConsultUnavailableException(message);
        }

        AnthropicMessageResponse? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Anthropic response for taper {TaperEventId}", taperEvent.Id);
            throw new TaperConsultUnavailableException("AI consult is temporarily unavailable — try again shortly.");
        }

        var answer = parsed?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new TaperConsultUnavailableException("AI consult is temporarily unavailable — try again shortly.");
        }

        return (answer, comparedToPriorTaper);
    }

    private async Task<(string SystemPrompt, bool ComparedToPriorTaper)> BuildSystemPromptAsync(Tournament taperEvent, DateOnly today)
    {
        var daysUntil = (taperEvent.StartDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        var phase = TaperPhaseCalculator.Compute(daysUntil, taperEvent.TaperDays!.Value, taperEvent.Name);

        var (windowStart, windowEnd) = TaperPhaseCalculator.CheckInWindow(taperEvent.StartDate, taperEvent.TaperDays!.Value);

        var thisTaperCheckIns = await _db.TaperCheckIns
            .Where(c => c.TournamentId == taperEvent.Id)
            .OrderBy(c => c.Date)
            .ToListAsync();

        var tapersCompleted = await _db.Tournaments
            .CountAsync(t => t.TaperDays != null && t.Id != taperEvent.Id && t.StartDate < today);

        var sb = new StringBuilder();
        sb.AppendLine("You are a taper-coaching assistant for a competitive Ultimate Frisbee athlete using their own personal training tracker.");
        sb.AppendLine("You draw on established taper-research knowledge (progressive volume reduction while holding intensity/frequency steady), applied to this athlete's own real data below.");
        sb.AppendLine("Never contradict the deterministic taper phase/target guidance already shown to the athlete elsewhere in the app — you are explanatory, personalized context alongside it, not a replacement for it.");
        sb.AppendLine();
        sb.AppendLine($"Tournament: {taperEvent.Name ?? "unnamed"} on {taperEvent.StartDate:yyyy-MM-dd}. Taper length: {taperEvent.TaperDays} days.");
        sb.AppendLine($"Current phase: {phase.Phase}. Deterministic guidance: {phase.Message}");
        sb.AppendLine();

        sb.AppendLine($"tapersCompleted = {tapersCompleted}");
        if (tapersCompleted == 0)
        {
            sb.AppendLine("This is the athlete's first tracked taper. Do not reference or assume any prior taper history — there is none. Lean on general taper-science guidance and what's in this taper's own check-ins so far.");
        }

        sb.AppendLine();
        sb.AppendLine("This taper's daily check-ins (energy/soreness/motivation are 1-5 scales; dates after the tournament date are post-event debrief entries; \"missing\" means no check-in was recorded for that date — treat a gap as meaningful, e.g. busy/unmotivated/forgot, never as a neutral or good day):");
        sb.AppendLine(FormatCheckInWindow(windowStart, windowEnd, taperEvent.StartDate, thisTaperCheckIns));

        var comparedToPriorTaper = false;
        if (tapersCompleted > 0)
        {
            var priorEvent = await _db.Tournaments
                .Where(t => t.TaperDays != null && t.Id != taperEvent.Id && t.StartDate < today)
                .OrderByDescending(t => t.StartDate)
                .FirstOrDefaultAsync();

            if (priorEvent is not null)
            {
                var priorCheckIns = await _db.TaperCheckIns
                    .Where(c => c.TournamentId == priorEvent.Id)
                    .OrderBy(c => c.Date)
                    .ToListAsync();

                if (priorCheckIns.Count > 0)
                {
                    comparedToPriorTaper = true;
                    var (priorWindowStart, priorWindowEnd) = TaperPhaseCalculator.CheckInWindow(priorEvent.StartDate, priorEvent.TaperDays!.Value);

                    sb.AppendLine();
                    sb.AppendLine($"Most recent completed taper for comparison ({priorEvent.Name ?? "unnamed"}, {priorEvent.StartDate:yyyy-MM-dd}):");
                    sb.AppendLine(FormatCheckInWindow(priorWindowStart, priorWindowEnd, priorEvent.StartDate, priorCheckIns));

                    var withData = priorCheckIns.Where(c => c.Date <= priorEvent.StartDate).ToList();
                    if (withData.Count > 0)
                    {
                        sb.AppendLine($"Prior taper averages (daily check-ins only, excludes debrief): energy {withData.Average(c => c.Energy):F1}, soreness {withData.Average(c => c.Soreness):F1}, motivation {withData.Average(c => c.Motivation):F1}.");
                    }
                }
            }
        }

        // Best-effort: session/exercise notes already exist in the app and are
        // otherwise unused. A failure here must never block the consult itself.
        try
        {
            var sessionNotes = await _db.WorkoutSessions
                .Where(s => s.Date >= windowStart && s.Date <= windowEnd && s.Notes != null)
                .Select(s => new { s.Date, s.Notes })
                .ToListAsync();

            var exerciseNotes = await _db.ExerciseNotes
                .Include(n => n.WorkoutSession)
                .Include(n => n.Exercise)
                .Where(n => n.WorkoutSession.Date >= windowStart && n.WorkoutSession.Date <= windowEnd)
                .Select(n => new { n.WorkoutSession.Date, ExerciseName = n.Exercise.Name, n.Notes })
                .ToListAsync();

            if (sessionNotes.Count > 0 || exerciseNotes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Workout session/exercise notes logged during this window (may add color, e.g. how a session actually felt):");
                foreach (var n in sessionNotes)
                {
                    sb.AppendLine($"- {n.Date:yyyy-MM-dd} (session note): {n.Notes}");
                }
                foreach (var n in exerciseNotes)
                {
                    sb.AppendLine($"- {n.Date:yyyy-MM-dd} ({n.ExerciseName}): {n.Notes}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pull session/exercise notes for taper consult, continuing without them");
        }

        sb.AppendLine();
        sb.AppendLine("Answer the athlete's question below, grounded only in the data above. Keep the answer concise and practical.");

        return (sb.ToString(), comparedToPriorTaper);
    }

    private static string FormatCheckInWindow(DateOnly start, DateOnly end, DateOnly eventDate, List<TaperCheckIn> checkIns)
    {
        var byDate = checkIns.ToDictionary(c => c.Date);
        var sb = new StringBuilder();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var isDebrief = date > eventDate;
            var label = isDebrief ? "debrief" : "taper day";
            if (byDate.TryGetValue(date, out var c))
            {
                var context = string.IsNullOrWhiteSpace(c.Context) ? "" : $", note: \"{c.Context}\"";
                sb.AppendLine($"- {date:yyyy-MM-dd} ({label}): energy {c.Energy}, soreness {c.Soreness}, motivation {c.Motivation}{context}");
            }
            else
            {
                sb.AppendLine($"- {date:yyyy-MM-dd} ({label}): missing — no check-in recorded");
            }
        }
        return sb.ToString();
    }

    private class AnthropicMessageResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
