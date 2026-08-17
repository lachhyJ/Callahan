using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// One non-guilt push reminder per taper event per day, if today's check-in is
// still missing once local time passes ReminderHour. No BackgroundService/hosted
// job pattern existed in this app before this — kept deliberately simple (a
// coarse poll loop) rather than introducing real scheduling infra, matching the
// app's existing tolerance for "good enough for a single-instance personal app"
// (see RestTimerController's in-memory timer dictionary).
public class TaperReminderService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);
    private const int ReminderHour = 20; // 20:00 local — see docker-compose TZ note

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaperReminderService> _logger;

    public TaperReminderService(IServiceScopeFactory scopeFactory, ILogger<TaperReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendAsync();
            }
            catch (Exception ex)
            {
                // Never let one bad poll kill the hosted service — log and retry
                // next interval.
                _logger.LogError(ex, "Taper reminder check failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // App shutting down.
            }
        }
    }

    private async Task CheckAndSendAsync()
    {
        var now = DateTime.Now;
        if (now.Hour < ReminderHour) return;

        var today = DateOnly.FromDateTime(now);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

        // Active window (see TaperPhaseCalculator.CheckInWindow) — can't be
        // expressed as a single translatable EF Core predicate since it's a
        // shared C# helper, so the AddDays calls here must still mirror it by
        // hand; kept inline rather than loading every event into memory to
        // filter client-side.
        var candidates = await db.TaperEvents
            .Where(e => today >= e.Date.AddDays(-e.TaperDays) && today <= e.Date.AddDays(3))
            .ToListAsync();

        if (candidates.Count == 0) return;

        List<Models.PushSubscription>? subscriptions = null;

        foreach (var taperEvent in candidates)
        {
            var hasCheckIn = await db.TaperCheckIns.AnyAsync(c => c.TaperEventId == taperEvent.Id && c.Date == today);
            if (hasCheckIn) continue;

            var alreadySent = await db.TaperReminderLogs.AnyAsync(r => r.TaperEventId == taperEvent.Id && r.Date == today);
            if (alreadySent) continue;

            subscriptions ??= await db.PushSubscriptions.ToListAsync();
            if (subscriptions.Count == 0) continue;

            var isDebrief = today > taperEvent.Date;
            var title = isDebrief ? "Taper debrief" : "Taper check-in";
            var body = isDebrief
                ? $"Haven't logged your debrief for {taperEvent.Name ?? "your tournament"} yet — how'd it go?"
                : $"Haven't logged today's taper check-in for {taperEvent.Name ?? "your tournament"} yet.";

            await pushService.SendToAllAsync(subscriptions, title, body);

            db.TaperReminderLogs.Add(new TaperReminderLog { TaperEventId = taperEvent.Id, Date = today });
            await db.SaveChangesAsync();
        }
    }
}
