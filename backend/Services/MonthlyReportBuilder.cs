using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Services;

// Builds the deterministic monthly report described in the feature spec.
// No AI/LLM layer — every section is a rules table or an aggregation over
// live data. Stalls/movers deliberately look at each exercise's most recent
// 6-8 sessions across ALL history (not clipped to the calendar month) before
// filtering down to what's relevant to display for this month.
public class MonthlyReportBuilder
{
    private readonly AppDbContext _db;
    private const int StallWindow = 8;
    private const decimal StallThresholdPercent = 3m; // flat = < 3% e1RM range across the window

    public MonthlyReportBuilder(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MonthlyReportDto> BuildAsync(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = monthEnd.Day;
        var weeksInMonth = (monthEnd.DayNumber - monthStart.DayNumber + 1) / 7m;

        var trailingStart = monthStart.AddMonths(-3);
        var trailingEndExclusive = monthStart; // 3 full months immediately before this one
        var trailingWeeks = (trailingEndExclusive.DayNumber - trailingStart.DayNumber) / 7m;

        var workouts = await _db.WorkoutSessions
            .Include(s => s.WorkoutTemplate)
            .Include(s => s.Sets)
            .Where(s => s.Date >= trailingStart && s.Date <= monthEnd)
            .ToListAsync();

        var runsAndUltimate = await _db.Activities
            .Include(a => a.RunSessionType)
            .Where(a => a.Date >= trailingStart && a.Date <= monthEnd)
            .ToListAsync();

        var monthWorkouts = workouts.Where(w => w.Date >= monthStart && w.Date <= monthEnd).ToList();
        var monthActivities = runsAndUltimate.Where(a => a.Date >= monthStart && a.Date <= monthEnd).ToList();
        var trailingWorkouts = workouts.Where(w => w.Date >= trailingStart && w.Date < trailingEndExclusive).ToList();
        var trailingActivities = runsAndUltimate.Where(a => a.Date >= trailingStart && a.Date < trailingEndExclusive).ToList();

        var taperEvents = await _db.TaperEvents.ToListAsync();

        var consistency = BuildConsistency(monthWorkouts, monthActivities, trailingWorkouts, trailingActivities, weeksInMonth, trailingWeeks, daysInMonth);
        var loadProgression = await BuildLoadProgressionAsync(monthStart, monthEnd);
        var running = BuildRunning(monthActivities);
        var balance = await BuildBalanceAsync(monthStart, monthEnd);
        var context = BuildContext(monthWorkouts, monthActivities, taperEvents, monthStart, monthEnd);
        var taperOverlaps = await BuildTaperOverlapsAsync(taperEvents, monthStart, monthEnd);
        var headline = BuildHeadline(consistency);
        var nextMonth = BuildNextMonthQuestions(loadProgression, context, consistency);

        return new MonthlyReportDto(
            year, month, false, true, DateTime.UtcNow, null,
            headline, consistency, loadProgression, running, balance, context, taperOverlaps, nextMonth);
    }

    private static ConsistencySectionDto BuildConsistency(
        List<WorkoutSession> monthWorkouts, List<Activity> monthActivities,
        List<WorkoutSession> trailingWorkouts, List<Activity> trailingActivities,
        decimal weeksInMonth, decimal trailingWeeks, int daysInMonth)
    {
        var totalSessions = monthWorkouts.Count + monthActivities.Count;
        var trailingTotal = trailingWorkouts.Count + trailingActivities.Count;
        var sessionsPerWeek = weeksInMonth > 0 ? totalSessions / weeksInMonth : 0m;
        var trailingPerWeek = trailingWeeks > 0 ? trailingTotal / trailingWeeks : 0m;

        var byType = new List<SessionTypeCountDto>();
        foreach (var g in monthWorkouts.GroupBy(w => w.WorkoutTemplate?.Name ?? "Manual"))
        {
            byType.Add(new SessionTypeCountDto(g.Key, g.Count()));
        }
        foreach (var g in monthActivities.Where(a => a.Type == ActivityType.Running).GroupBy(a => a.RunSessionType?.Name ?? "Unspecified run"))
        {
            byType.Add(new SessionTypeCountDto(g.Key, g.Count()));
        }
        var ultimateCount = monthActivities.Count(a => a.Type == ActivityType.Ultimate);
        if (ultimateCount > 0) byType.Add(new SessionTypeCountDto("Ultimate", ultimateCount));

        var buckets = WeeklyConsistencyService.BucketByWeek(
            monthWorkouts.Select(w => w.Date).ToList(),
            monthActivities.Where(a => a.Type == ActivityType.Running).Select(a => a.Date).ToList());

        var weeklyTargets = WeeklyConsistencyService.Definitions.Select(def =>
        {
            var weeksHit = buckets.Values.Count(v => def.Qualifies(v.Gym, v.Run));
            return new WeeklyTargetHitDto(def.Type, def.Label, weeksHit, buckets.Count);
        }).ToList();

        var daysTrained = monthWorkouts.Select(w => w.Date).Concat(monthActivities.Select(a => a.Date)).Distinct().Count();

        return new ConsistencySectionDto(totalSessions, weeksInMonth, sessionsPerWeek, trailingPerWeek, byType, weeklyTargets, daysTrained, daysInMonth);
    }

    private async Task<LoadProgressionSectionDto> BuildLoadProgressionAsync(DateOnly monthStart, DateOnly monthEnd)
    {
        var allSets = await _db.ExerciseSets
            .Where(s => s.SetType != SetType.Warmup)
            .Include(s => s.WorkoutSession)
            .Include(s => s.Exercise)
            .ToListAsync();

        // PRs: this month's best e1RM per exercise that exceeds every set
        // logged before the month started (i.e. a genuine new all-time best,
        // not just the best-of-the-month).
        var prs = new List<PrDto>();
        foreach (var g in allSets.GroupBy(s => s.ExerciseId))
        {
            var before = g.Where(s => s.WorkoutSession.Date < monthStart).ToList();
            var inMonth = g.Where(s => s.WorkoutSession.Date >= monthStart && s.WorkoutSession.Date <= monthEnd).ToList();
            if (inMonth.Count == 0) continue;

            var beforeMax = before.Count > 0 ? before.Max(E1Rm) : 0m;
            var monthBest = inMonth.OrderByDescending(E1Rm).First();
            var monthBestE1Rm = E1Rm(monthBest);
            if (monthBestE1Rm > beforeMax)
            {
                prs.Add(new PrDto(monthBest.ExerciseId, monthBest.Exercise.Name, monthBestE1Rm, monthBest.WorkoutSession.Date));
            }
        }

        // Stalls / movers: rolling last StallWindow sessions per exercise,
        // across all history, independent of the month boundary. A
        // "session" here is one WorkoutSession that logged this exercise;
        // the value compared per session is that session's best e1RM set.
        var movers = new List<MoverDto>();
        var stalls = new List<StallDto>();

        foreach (var g in allSets.GroupBy(s => s.ExerciseId))
        {
            var perSession = g.GroupBy(s => s.WorkoutSessionId)
                .Select(sg => new { Date = sg.First().WorkoutSession.Date, BestE1Rm = sg.Max(E1Rm) })
                .OrderBy(x => x.Date)
                .ToList();

            if (perSession.Count < 3) continue; // not enough history to call it either way

            var window = perSession.Skip(Math.Max(0, perSession.Count - StallWindow)).ToList();
            var mostRecentDate = window[^1].Date;

            // Only display if the window's most recent qualifying session
            // falls within (or shortly after) the report month — the
            // computation window itself is never clipped to the month.
            var nearMonth = mostRecentDate >= monthStart && mostRecentDate <= monthEnd.AddDays(7);
            if (!nearMonth) continue;

            var first = window[0].BestE1Rm;
            var last = window[^1].BestE1Rm;
            if (first <= 0) continue;

            var deltaPercent = (last - first) / first * 100m;
            var range = window.Max(x => x.BestE1Rm) - window.Min(x => x.BestE1Rm);
            var rangePercent = first > 0 ? range / first * 100m : 0m;

            var exerciseName = g.First().Exercise.Name;
            if (window.Count >= 4 && rangePercent < StallThresholdPercent)
            {
                stalls.Add(new StallDto(g.Key, exerciseName, window.Count, mostRecentDate));
            }
            else if (Math.Abs(deltaPercent) >= StallThresholdPercent)
            {
                movers.Add(new MoverDto(g.Key, exerciseName, first, last, Math.Round(deltaPercent, 1)));
            }
        }

        movers = movers.OrderByDescending(m => Math.Abs(m.DeltaPercent)).Take(10).ToList();
        stalls = stalls.OrderByDescending(s => s.SessionsFlat).Take(10).ToList();

        // Program exercises with zero logged (non-warmup) sets this month.
        var programExerciseIds = await _db.WorkoutTemplateExercises
            .Include(te => te.Exercise)
            .Select(te => new { te.ExerciseId, te.Exercise.Name })
            .Distinct()
            .ToListAsync();

        var loggedThisMonthIds = allSets
            .Where(s => s.WorkoutSession.Date >= monthStart && s.WorkoutSession.Date <= monthEnd)
            .Select(s => s.ExerciseId)
            .ToHashSet();

        var zeroSet = programExerciseIds
            .Where(pe => !loggedThisMonthIds.Contains(pe.ExerciseId))
            .Select(pe => pe.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        return new LoadProgressionSectionDto(
            prs.OrderByDescending(p => p.Date).ToList(),
            movers, stalls, zeroSet);
    }

    // Epley formula: 1RM = weight * (1 + reps/30).
    private static decimal E1Rm(ExerciseSet s) => s.WeightKg * (1 + s.Reps / 30m);

    private static RunningSectionDto BuildRunning(List<Activity> monthActivities)
    {
        var byType = monthActivities
            .Where(a => a.Type == ActivityType.Running)
            .GroupBy(a => a.RunSessionType?.Name ?? "Unspecified run")
            .Select(g => new RunTypeSummaryDto(
                g.Key, g.Count(),
                g.Where(a => a.DistanceKm != null).Sum(a => a.DistanceKm!.Value),
                g.Sum(a => a.DurationSeconds)))
            .OrderByDescending(r => r.Count)
            .ToList();

        return new RunningSectionDto(byType);
    }

    private async Task<BalanceSectionDto> BuildBalanceAsync(DateOnly monthStart, DateOnly monthEnd)
    {
        var sets = await _db.ExerciseSets
            .Where(s => s.SetType != SetType.Warmup && s.WorkoutSession.Date >= monthStart && s.WorkoutSession.Date <= monthEnd)
            .Include(s => s.Exercise)
            .ToListAsync();

        if (sets.Count == 0) return new BalanceSectionDto(null);

        var pushCount = sets.Count(s => s.Exercise.Category == ExerciseCategory.Push);
        var pullCount = sets.Count(s => s.Exercise.Category == ExerciseCategory.Pull);

        if (pushCount == 0 || pullCount == 0) return new BalanceSectionDto(null);

        var higher = Math.Max(pushCount, pullCount);
        var lower = Math.Min(pushCount, pullCount);
        var pctBelow = (higher - lower) / (decimal)higher * 100m;

        if (pctBelow < 20m) return new BalanceSectionDto(null); // in balance, nothing worth flagging

        var lowerLabel = pushCount < pullCount ? "Push" : "Pull";
        var higherLabel = pushCount < pullCount ? "Pull" : "Push";
        return new BalanceSectionDto($"{lowerLabel} volume {Math.Round(pctBelow)}% below {higherLabel} this month");
    }

    private static ContextSectionDto BuildContext(
        List<WorkoutSession> monthWorkouts, List<Activity> monthActivities,
        List<TaperEvent> taperEvents, DateOnly monthStart, DateOnly monthEnd)
    {
        var tournaments = taperEvents
            .Where(e => e.Date >= monthStart && e.Date <= monthEnd)
            .OrderBy(e => e.Date)
            .Select(e => e.Name ?? $"Event on {e.Date:yyyy-MM-dd}")
            .ToList();

        var allDates = monthWorkouts.Select(w => w.Date).Concat(monthActivities.Select(a => a.Date)).Distinct().OrderBy(d => d).ToList();

        int? longestGap = null;
        DateOnly? gapStart = null, gapEnd = null;
        for (var i = 1; i < allDates.Count; i++)
        {
            var gap = allDates[i].DayNumber - allDates[i - 1].DayNumber;
            if (longestGap is null || gap > longestGap)
            {
                longestGap = gap;
                gapStart = allDates[i - 1];
                gapEnd = allDates[i];
            }
        }

        return new ContextSectionDto(tournaments, longestGap, gapStart, gapEnd);
    }

    private async Task<List<TaperSectionDto>> BuildTaperOverlapsAsync(List<TaperEvent> taperEvents, DateOnly monthStart, DateOnly monthEnd)
    {
        var results = new List<TaperSectionDto>();

        foreach (var ev in taperEvents)
        {
            var taperStart = ev.Date.AddDays(-ev.TaperDays);
            var taperEnd = ev.Date;
            // No overlap with the report month at all — omit entirely.
            if (taperEnd < monthStart || taperStart > monthEnd) continue;

            var overlapStart = taperStart > monthStart ? taperStart : monthStart;
            var overlapEnd = taperEnd < monthEnd ? taperEnd : monthEnd;
            var overlapDays = overlapEnd.DayNumber - overlapStart.DayNumber + 1;
            var monthDays = monthEnd.DayNumber - monthStart.DayNumber + 1;
            var overlapFraction = (decimal)overlapDays / monthDays;

            // Partial vs substantial: substantial once the taper window
            // covers at least a third of the report month.
            var overlap = overlapFraction >= (1m / 3m) ? "substantial" : "partial";

            var monthSets = await _db.ExerciseSets
                .Where(s => s.SetType != SetType.Warmup && s.WorkoutSession.Date >= monthStart && s.WorkoutSession.Date <= monthEnd)
                .Include(s => s.WorkoutSession)
                .ToListAsync();
            var monthRuns = await _db.Activities
                .Where(a => a.Type == ActivityType.Running && a.Date >= monthStart && a.Date <= monthEnd)
                .ToListAsync();

            var weeksInMonth = (monthEnd.DayNumber - monthStart.DayNumber + 1) / 7m;
            var rawSessionsPerWeek = weeksInMonth > 0
                ? (monthSets.Select(s => s.WorkoutSessionId).Distinct().Count() + monthRuns.Count) / weeksInMonth
                : 0m;

            var exclTaperSets = monthSets.Where(s => s.WorkoutSession.Date < taperStart || s.WorkoutSession.Date > taperEnd).ToList();
            var exclTaperRuns = monthRuns.Where(a => a.Date < taperStart || a.Date > taperEnd).ToList();
            var exclDays = monthDays - overlapDays;
            var exclWeeks = exclDays / 7m;
            var exclSessionsPerWeek = exclWeeks > 0
                ? (exclTaperSets.Select(s => s.WorkoutSessionId).Distinct().Count() + exclTaperRuns.Count) / exclWeeks
                : 0m;

            // Actual reduction: baseline weekly gym volume (4 weeks before
            // the taper window) vs actual weekly volume during the taper
            // window that overlaps this month — mirrors TaperController's
            // baseline pattern.
            var baselineStart = taperStart.AddDays(-28);
            var baselineSets = await _db.ExerciseSets
                .Where(s => s.WorkoutSession.Date >= baselineStart && s.WorkoutSession.Date < taperStart)
                .Include(s => s.WorkoutSession)
                .ToListAsync();
            var baselineWeeklyVolume = baselineSets.Sum(s => s.WeightKg * s.Reps) / 4m;

            var taperWindowSets = await _db.ExerciseSets
                .Where(s => s.WorkoutSession.Date >= overlapStart && s.WorkoutSession.Date <= overlapEnd)
                .Include(s => s.WorkoutSession)
                .ToListAsync();
            var taperWindowWeeks = Math.Max(overlapDays / 7m, 0.1m);
            var taperWeeklyVolume = taperWindowSets.Sum(s => s.WeightKg * s.Reps) / taperWindowWeeks;

            decimal? actualReduction = baselineWeeklyVolume > 0
                ? (1m - taperWeeklyVolume / baselineWeeklyVolume) * 100m
                : null;

            var (checkInWindowStart, checkInWindowEnd) = TaperPhaseCalculator.CheckInWindow(ev.Date, ev.TaperDays);
            var expectedCheckInDays = checkInWindowEnd.DayNumber - checkInWindowStart.DayNumber + 1;
            var actualCheckIns = await _db.TaperCheckIns.CountAsync(c => c.TaperEventId == ev.Id);

            results.Add(new TaperSectionDto(
                ev.Name ?? $"Event on {ev.Date:yyyy-MM-dd}", ev.Date, overlap,
                ev.PlannedReductionPercent * 100m, actualReduction.HasValue ? Math.Round(actualReduction.Value, 1) : null,
                actualCheckIns, expectedCheckInDays,
                Math.Round(rawSessionsPerWeek, 2), Math.Round(exclSessionsPerWeek, 2)));
        }

        return results;
    }

    private static string BuildHeadline(ConsistencySectionDto c)
    {
        var weeksFmt = c.WeeksInMonth.ToString("0.0");
        var perWeekFmt = c.SessionsPerWeek.ToString("0.0");
        var trailingFmt = c.TrailingSessionsPerWeek.ToString("0.0");
        return $"{c.TotalSessions} session{(c.TotalSessions == 1 ? "" : "s")} across {weeksFmt} weeks ({perWeekFmt}/wk vs {trailingFmt}/wk trailing average).";
    }

    private static List<string> BuildNextMonthQuestions(LoadProgressionSectionDto load, ContextSectionDto context, ConsistencySectionDto consistency)
    {
        var questions = new List<string>();

        if (load.Stalls.Count > 0)
        {
            var name = load.Stalls[0].ExerciseName;
            questions.Add($"{name} hasn't moved in {load.Stalls[0].SessionsFlat} sessions — worth a program change, or just a plateau to push through?");
        }

        if (load.ZeroSetProgramExercises.Count > 0)
        {
            var name = load.ZeroSetProgramExercises[0];
            var extra = load.ZeroSetProgramExercises.Count > 1 ? $" (and {load.ZeroSetProgramExercises.Count - 1} more)" : "";
            questions.Add($"{name}{extra} didn't get logged at all this month — dropped on purpose, or just slipping?");
        }

        var typeCounts = consistency.SessionsByType;
        if (typeCounts.Count > 1)
        {
            var max = typeCounts.Max(t => t.Count);
            var min = typeCounts.Min(t => t.Count);
            if (max >= min * 2 && max >= 2)
            {
                var maxType = typeCounts.First(t => t.Count == max).Label;
                var minType = typeCounts.First(t => t.Count == min).Label;
                questions.Add($"{maxType} ran {max}x this month vs {minType}'s {min}x — deliberate, or worth rebalancing?");
            }
        }

        if (questions.Count < 3 && context.LongestGapDays is > 7)
        {
            questions.Add($"Longest gap this month was {context.LongestGapDays} days ({context.LongestGapStart:MMM d}–{context.LongestGapEnd:MMM d}) — what happened there?");
        }

        return questions.Take(3).ToList();
    }
}
