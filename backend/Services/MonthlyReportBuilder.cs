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
            .Include(a => a.ActivitySessionType)
            .Where(a => a.Date >= trailingStart && a.Date <= monthEnd)
            .ToListAsync();

        var monthWorkouts = workouts.Where(w => w.Date >= monthStart && w.Date <= monthEnd).ToList();
        var monthActivities = runsAndUltimate.Where(a => a.Date >= monthStart && a.Date <= monthEnd).ToList();
        var trailingWorkouts = workouts.Where(w => w.Date >= trailingStart && w.Date < trailingEndExclusive).ToList();
        var trailingActivities = runsAndUltimate.Where(a => a.Date >= trailingStart && a.Date < trailingEndExclusive).ToList();

        var taperEvents = await _db.TaperEvents.ToListAsync();

        var wellnessRows = (await _db.DailyWellness
                .Where(w => w.Date >= trailingStart && w.Date <= monthEnd)
                .ToListAsync())
            .Select(WellnessMapping.ToDto)
            .ToList();
        var monthWellness = wellnessRows.Where(w => w.Date >= monthStart && w.Date <= monthEnd).ToList();
        var trailingWellness = wellnessRows.Where(w => w.Date >= trailingStart && w.Date < trailingEndExclusive).ToList();

        var consistency = BuildConsistency(monthWorkouts, monthActivities, trailingWorkouts, trailingActivities, weeksInMonth, trailingWeeks, daysInMonth);
        var loadProgression = await BuildLoadProgressionAsync(monthStart, monthEnd);
        var running = await BuildRunningAsync(monthStart, monthEnd, monthActivities);
        var balance = await BuildBalanceAsync(monthStart, monthEnd);
        var context = BuildContext(monthWorkouts, monthActivities, taperEvents, monthStart, monthEnd);
        var taperOverlaps = await BuildTaperOverlapsAsync(taperEvents, monthStart, monthEnd);
        var wellness = MonthlyWellnessSummarizer.Summarize(monthWellness, trailingWellness, daysInMonth);
        var headline = BuildHeadline(consistency, loadProgression);
        var nextMonth = BuildNextMonthQuestions(loadProgression, context, consistency);

        return new MonthlyReportDto(
            year, month, false, true, DateTime.UtcNow, null,
            headline, consistency, loadProgression, running, balance, context, taperOverlaps, nextMonth, wellness);
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

        // All three families break down the same way - Ultimate's session
        // types (Solo / Throws / Pod / Club Training / Game) are already
        // seeded and already attached, they just weren't being read.
        var byType = new List<SessionTypeCountDto>();
        foreach (var g in monthWorkouts.GroupBy(w => w.WorkoutTemplate?.Name ?? "Manual"))
        {
            byType.Add(new SessionTypeCountDto(g.Key, g.Count(), SessionFamily.Gym));
        }
        foreach (var g in monthActivities.Where(a => a.Type == ActivityType.Running).GroupBy(a => a.ActivitySessionType?.Name ?? "Unspecified run"))
        {
            byType.Add(new SessionTypeCountDto(g.Key, g.Count(), SessionFamily.Running));
        }
        foreach (var g in monthActivities.Where(a => a.Type == ActivityType.Ultimate).GroupBy(a => a.ActivitySessionType?.Name ?? "Unspecified"))
        {
            byType.Add(new SessionTypeCountDto(g.Key, g.Count(), SessionFamily.Ultimate));
        }

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

        // Each exercise is measured on the basis its own history supports —
        // e1RM for normal working sets, set volume for high-rep accessories,
        // load-then-reps for assisted/bodyweight work where e1RM is either
        // inverted or identically zero. See LiftProgress.
        var basisByExercise = allSets
            .GroupBy(s => s.ExerciseId)
            .ToDictionary(g => g.Key, g => LiftProgress.BasisFor(g.Select(ToInput).ToList()));

        // PRs: this month's best set per exercise that beats every set logged
        // before the month started — a genuine new all-time best, not just
        // the best of the month.
        var prs = new List<PrDto>();
        foreach (var g in allSets.GroupBy(s => s.ExerciseId))
        {
            var basis = basisByExercise[g.Key];
            var before = g.Where(s => s.WorkoutSession.Date < monthStart).Select(ToInput).ToList();
            var inMonth = g.Where(s => s.WorkoutSession.Date >= monthStart && s.WorkoutSession.Date <= monthEnd).ToList();
            if (inMonth.Count == 0) continue;

            var monthBestSet = inMonth
                .OrderByDescending(x => LiftProgress.Score(ToInput(x), basis))
                .First();
            var monthBest = ToInput(monthBestSet);
            var previousBest = LiftProgress.Best(before, basis);

            var beats = previousBest is null
                || LiftProgress.Score(monthBest, basis) > LiftProgress.Score(previousBest, basis);
            if (!beats) continue;

            prs.Add(new PrDto(
                monthBestSet.ExerciseId, monthBestSet.Exercise.Name,
                LiftProgress.ToDto(monthBest, basis),
                previousBest is null ? null : LiftProgress.ToDto(previousBest, basis),
                basis.ToString(), monthBestSet.WorkoutSession.Date));
        }

        // Stalls / movers: rolling last StallWindow sessions per exercise,
        // across all history, independent of the month boundary. A "session"
        // here is one WorkoutSession that logged this exercise; the value
        // compared per session is that session's best set on the exercise's
        // own basis.
        var movers = new List<MoverDto>();
        var stalls = new List<StallDto>();

        foreach (var g in allSets.GroupBy(s => s.ExerciseId))
        {
            var basis = basisByExercise[g.Key];

            var perSession = g.GroupBy(s => s.WorkoutSessionId)
                .Select(sg => new
                {
                    Date = sg.First().WorkoutSession.Date,
                    Best = LiftProgress.Best(sg.Select(ToInput).ToList(), basis)!,
                })
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

            var windowSets = window.Select(x => x.Best).ToList();
            var first = windowSets[0];
            var last = windowSets[^1];
            var exerciseName = g.First().Exercise.Name;

            if (window.Count >= 4 && LiftProgress.IsFlat(windowSets, basis, StallThresholdPercent))
            {
                stalls.Add(new StallDto(
                    g.Key, exerciseName, window.Count, mostRecentDate,
                    LiftProgress.ToDto(last, basis), basis.ToString()));
                continue;
            }

            var deltaPercent = LiftProgress.DeltaPercent(first, last, basis);

            // Assisted lifts get no percentage (the score is a composite
            // ordering, not a magnitude), so "did it move" is judged on the
            // sets themselves.
            var moved = deltaPercent is null
                ? first.WeightKg != last.WeightKg || first.Reps != last.Reps
                : Math.Abs(deltaPercent.Value) >= StallThresholdPercent;

            if (moved)
            {
                movers.Add(new MoverDto(
                    g.Key, exerciseName,
                    LiftProgress.ToDto(first, basis), LiftProgress.ToDto(last, basis),
                    deltaPercent is null ? null : Math.Round(deltaPercent.Value, 1),
                    basis.ToString(), mostRecentDate));
            }
        }

        // Percentage movers rank by magnitude; assisted ones have no
        // percentage, so they sort after but are still shown.
        movers = movers.OrderByDescending(m => m.DeltaPercent is null ? 0m : Math.Abs(m.DeltaPercent.Value)).Take(10).ToList();
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
            movers, stalls, zeroSet, StallWindow);
    }

    private static LiftSetInput ToInput(ExerciseSet s) => new(s.WeightKg, s.Reps);

    private async Task<RunningSectionDto> BuildRunningAsync(
        DateOnly monthStart, DateOnly monthEnd, List<Activity> monthActivities)
    {
        // Work-rep counts, as a projection - no lap rows are materialised.
        var activeLapCounts = await _db.ActivityLaps
            .Where(l => l.IntensityType == ActivityLap.ActiveIntensityType
                     && l.Activity.Date >= monthStart && l.Activity.Date <= monthEnd)
            .GroupBy(l => l.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

        var runs = monthActivities
            .Where(a => a.Type == ActivityType.Running)
            .Select(a => new RunActivityInput(
                a.ActivitySessionType?.Name ?? "Unspecified run",
                a.DistanceKm,
                a.DurationSeconds,
                a.HighSpeedDistanceM,
                activeLapCounts.TryGetValue(a.Id, out var laps) ? laps : 0));

        return new RunningSectionDto(RunningMetrics.Summarize(runs));
    }

    // Push/pull as execution drift against what the month's own sessions
    // prescribed, not as a raw ratio - see PushPullBalance for why the raw
    // ratio measured the program rather than the training. Manual sessions
    // (no template) are excluded: there's no plan to compare them against.
    private async Task<BalanceSectionDto> BuildBalanceAsync(DateOnly monthStart, DateOnly monthEnd)
    {
        var templatedSessions = await _db.WorkoutSessions
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd && s.WorkoutTemplateId != null)
            .Select(s => new { s.Id, TemplateId = s.WorkoutTemplateId!.Value })
            .ToListAsync();

        if (templatedSessions.Count == 0) return new BalanceSectionDto(null);

        var sessionIds = templatedSessions.Select(s => s.Id).ToHashSet();

        // Prescribed: each template's target sets by category, counted once
        // per time that template was actually run this month.
        var runsPerTemplate = templatedSessions
            .GroupBy(s => s.TemplateId)
            .ToDictionary(g => g.Key, g => g.Count());

        var templateTargets = await _db.WorkoutTemplateExercises
            .Where(te => runsPerTemplate.Keys.Contains(te.WorkoutTemplateId))
            .Select(te => new { te.WorkoutTemplateId, te.TargetSets, te.Exercise.Category })
            .ToListAsync();

        var plannedPush = templateTargets
            .Where(t => t.Category == ExerciseCategory.Push)
            .Sum(t => t.TargetSets * runsPerTemplate[t.WorkoutTemplateId]);
        var plannedPull = templateTargets
            .Where(t => t.Category == ExerciseCategory.Pull)
            .Sum(t => t.TargetSets * runsPerTemplate[t.WorkoutTemplateId]);

        // Logged: the same sessions, non-warmup sets only.
        var loggedCategories = await _db.ExerciseSets
            .Where(s => s.SetType != SetType.Warmup && sessionIds.Contains(s.WorkoutSessionId))
            .Select(s => s.Exercise.Category)
            .ToListAsync();

        var actualPush = loggedCategories.Count(c => c == ExerciseCategory.Push);
        var actualPull = loggedCategories.Count(c => c == ExerciseCategory.Pull);

        return new BalanceSectionDto(
            PushPullBalance.Flag(new PushPullInput(plannedPush, plannedPull, actualPush, actualPull)));
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

    private static string BuildHeadline(ConsistencySectionDto c, LoadProgressionSectionDto load)
    {
        var weeksFmt = c.WeeksInMonth.ToString("0.0");
        var perWeekFmt = c.SessionsPerWeek.ToString("0.0");
        var trailingFmt = c.TrailingSessionsPerWeek.ToString("0.0");
        var detail = $"{c.TotalSessions} session{(c.TotalSessions == 1 ? "" : "s")} across {weeksFmt} weeks ({perWeekFmt}/wk vs {trailingFmt}/wk trailing average).";
        return $"{ClassifyMonth(c, load)} — {detail}";
    }

    // Deterministic month verdict — the report's job is to "make a call", not
    // restate the numbers. Consistency vs the trailing rate is the spine: a
    // genuine drop in training frequency is the only thing that makes a month
    // "Down". Stalls / PRs / net mover direction only decide Strong vs Steady —
    // a high-volume month full of plateaus is Steady, not Down. Thresholds are
    // tunable; kept pure and public so it unit-tests directly.
    public static string ClassifyMonth(ConsistencySectionDto c, LoadProgressionSectionDto load)
    {
        var trailing = c.TrailingSessionsPerWeek;
        var moversNetPositive = load.Movers.Sum(m => m.DeltaPercent) > 0;

        if (trailing > 0 && c.SessionsPerWeek < trailing * 0.8m)
            return "Down month";

        if (c.SessionsPerWeek >= trailing && load.Stalls.Count == 0 && (load.Prs.Count > 0 || moversNetPositive))
            return "Strong month";

        return "Steady month";
    }

    // "Did X run twice as often as Y?" is only a real question when X and Y
    // are alternatives to each other. Comparing across families produced
    // nonsense - a club training is not a substitute for an interval session,
    // so their counts have no reason to match. Compared within a family only,
    // preferring Gym: templates are the genuinely interchangeable set.
    private const int MinCountForRebalanceQuestion = 3;

    private static readonly string[] RebalanceFamilyPreference =
        [SessionFamily.Gym, SessionFamily.Running, SessionFamily.Ultimate];

    public static string? RebalanceQuestion(List<SessionTypeCountDto> byType)
    {
        foreach (var family in RebalanceFamilyPreference)
        {
            var inFamily = byType.Where(t => t.Family == family).ToList();
            if (inFamily.Count < 2) continue;

            var max = inFamily.Max(t => t.Count);
            var min = inFamily.Min(t => t.Count);
            // 3, not 2: a 2x-vs-1x split clears the "twice as often" bar
            // arithmetically but is far too thin to ask a question about.
            if (max < MinCountForRebalanceQuestion || max < min * 2) continue;

            var maxType = inFamily.First(t => t.Count == max).Label;
            var minType = inFamily.First(t => t.Count == min).Label;
            return $"{maxType} ran {max}x this month vs {minType}'s {min}x — deliberate, or worth rebalancing?";
        }

        return null;
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

        var rebalance = RebalanceQuestion(consistency.SessionsByType);
        if (rebalance is not null) questions.Add(rebalance);

        if (questions.Count < 3 && context.LongestGapDays is > 7)
        {
            questions.Add($"Longest gap this month was {context.LongestGapDays} days ({context.LongestGapStart:MMM d}–{context.LongestGapEnd:MMM d}) — what happened there?");
        }

        return questions.Take(3).ToList();
    }
}
