using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api;

// Dev-only synthetic fixture data — gated by the same double condition as
// /api/auth/dev-login in Program.cs, never reachable in prod. Wipes and
// repopulates the generated tables (sessions, sets, activities, wellness,
// tournaments, reports) with plausible, entirely fake history so local
// tooling (Playwright visual baselines, manual UI checks) gets real-looking
// screens without ever needing a copy of real personal data. Catalog tables
// seeded by EF migrations — Exercises, WorkoutTemplates,
// WorkoutTemplateExercises, ActivitySessionTypes — are left untouched and
// read back here, so generated sessions follow the actual program structure
// rather than inventing their own.
public static class DevSeed
{
    public static async Task<DateOnly> RunAsync(AppDbContext db, TimeProvider time)
    {
        db.ExerciseSets.RemoveRange(db.ExerciseSets);
        db.WorkoutSessions.RemoveRange(db.WorkoutSessions);
        db.ActivityLaps.RemoveRange(db.ActivityLaps);
        db.ActivityTracks.RemoveRange(db.ActivityTracks);
        db.Activities.RemoveRange(db.Activities);
        db.Tournaments.RemoveRange(db.Tournaments);
        db.DailyWellness.RemoveRange(db.DailyWellness);
        db.MonthlyReports.RemoveRange(db.MonthlyReports);
        db.ExerciseNotes.RemoveRange(db.ExerciseNotes);
        await db.SaveChangesAsync();

        // Fixed seed, anchored on the injected clock: for the same "today" this
        // produces the same fixture every run. The e2e suite pins that clock
        // (Dev:FixedNow), so Playwright baselines don't drift day to day; on the
        // real clock the dates move, and so does which weekday the template
        // rotation starts on.
        // One stream per domain, not one shared stream: the workout loop draws a
        // number of values that depends on the catalog (how many template
        // exercises and sets exist), so on a shared stream every later domain -
        // wellness especially - shifted whenever the DB's catalog differed from
        // the one the baselines were recorded on. Separate streams keep runs,
        // games, Ultimate and wellness identical whatever the catalog holds.
        const int baseSeed = 20260904;
        var workoutRng = new Random(baseSeed + 1);
        var runRng = new Random(baseSeed + 2);
        var gameRng = new Random(baseSeed + 3);
        var ultimateRng = new Random(baseSeed + 4);
        var wellnessRng = new Random(baseSeed + 5);
        var today = time.Today();
        var windowStart = today.AddDays(-70);

        // Fake Garmin training metrics (see GarminActivityMetrics) on an
        // activity, scaled by a 0..1 intensity. ~1 in 8 is left unscored, like
        // real data where a short / no-HR session gets no training effect.
        void ApplyFakeGarminMetrics(Activity a, double intensity, Random rng)
        {
            if (rng.Next(8) == 0) return;
            a.ActivityTrainingLoad = Math.Round(30m + (decimal)(intensity * 95) + rng.Next(-8, 9), 0);
            a.AerobicTrainingEffect = Math.Round(1.4m + (decimal)(intensity * 2.6), 1);
            a.AnaerobicTrainingEffect = Math.Round(0.8m + (decimal)(rng.NextDouble() * 3.2), 1);
            a.TrainingEffectLabel = intensity > 0.66 ? "ANAEROBIC_CAPACITY"
                : intensity > 0.33 ? "AEROBIC_BASE" : "RECOVERY";
        }

        var templates = await db.WorkoutTemplates
            .Include(t => t.Exercises).ThenInclude(te => te.Exercise)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        if (templates.Count > 0)
        {
            var sessionDates = new List<DateOnly>();
            for (var d = windowStart; d < today; d = d.AddDays(1))
            {
                if (d.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday)
                    sessionDates.Add(d);
            }

            for (var i = 0; i < sessionDates.Count; i++)
            {
                var date = sessionDates[i];
                var template = templates[i % templates.Count];
                // 0 at the start of the window, 1 at the end — light progressive
                // overload so History/Trends has a real trend line, not flat noise.
                var progress = sessionDates.Count > 1 ? i / (double)(sessionDates.Count - 1) : 1.0;

                // Stored times are UTC; 17:30 is the intended wall-clock time in the app's current zone.
                var localStart = date.ToDateTime(new TimeOnly(17, 30)).AddMinutes(workoutRng.Next(-15, 15));
                var startedAt = TimeZoneInfo.ConvertTimeToUtc(localStart, time.LocalTimeZone);
                var session = new WorkoutSession
                {
                    Date = date,
                    Name = template.Name,
                    WorkoutTemplateId = template.Id,
                    StartedAt = startedAt,
                    FinishedAt = startedAt.AddMinutes(50 + workoutRng.Next(0, 25)),
                };

                foreach (var te in template.Exercises.OrderBy(e => e.ExerciseOrder))
                {
                    var baseWeight = BaseWeightFor(te.Exercise.Category, te.Exercise.IsAssisted);
                    var totalSets = te.WarmupSets + te.TargetSets;
                    for (var s = 0; s < totalSets; s++)
                    {
                        var isWarmup = s < te.WarmupSets;

                        // Time-held exercises log a hold, not a rep count: Reps
                        // and WeightKg stay 0 (the convention every volume/e1RM
                        // read site keys off DurationSeconds != null).
                        if (te.Exercise.IsTimeBased)
                        {
                            session.Sets.Add(new ExerciseSet
                            {
                                ExerciseId = te.ExerciseId,
                                SetOrder = s,
                                Reps = 0,
                                WeightKg = 0m,
                                DurationSeconds = te.TargetDurationSeconds ?? 30,
                                SetType = isWarmup ? SetType.Warmup : SetType.Normal,
                            });
                            continue;
                        }

                        var warmupFactor = isWarmup ? 0.5m + 0.15m * s : 1.0m;
                        var progressLoad = Math.Round((decimal)progress * 8m, 1);
                        var weight = te.Exercise.IsAssisted
                            ? -Math.Max(0m, baseWeight - progressLoad)
                            : Math.Round(baseWeight * warmupFactor + progressLoad, 1);

                        session.Sets.Add(new ExerciseSet
                        {
                            ExerciseId = te.ExerciseId,
                            SetOrder = s,
                            Reps = ParseTargetReps(te.TargetReps, workoutRng),
                            WeightKg = weight,
                            SetType = isWarmup ? SetType.Warmup : SetType.Normal,
                        });
                    }
                }

                db.WorkoutSessions.Add(session);
            }
        }

        var runTypes = await db.ActivitySessionTypes
            .Where(t => t.ActivityType == ActivityType.Running)
            .ToListAsync();
        for (var d = windowStart; d < today; d = d.AddDays(1))
        {
            var isRunDay = d.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Saturday;
            if (isRunDay && runRng.NextDouble() < 0.7)
            {
                var distanceKm = Math.Round(4m + (decimal)runRng.NextDouble() * 6m, 2);
                var runType = runTypes.Count > 0 ? runTypes[runRng.Next(runTypes.Count)] : null;
                var run = new Activity
                {
                    Date = d,
                    Type = ActivityType.Running,
                    Source = ActivitySource.Manual,
                    DurationSeconds = (int)(distanceKm * 60m * (5m + (decimal)runRng.NextDouble())),
                    DistanceKm = distanceKm,
                    Calories = (int)(distanceKm * 65m),
                    AvgHeartRate = 140 + runRng.Next(-10, 15),
                    ActivitySessionTypeId = runType?.Id,
                    // Keep the primary-in-SessionTags invariant true for seed data.
                    SessionTags = runType is null
                        ? new()
                        : new() { new ActivitySessionTag { ActivitySessionTypeId = runType.Id } },
                };
                ApplyFakeGarminMetrics(run, 0.35 + runRng.NextDouble() * 0.4, runRng);
                db.Activities.Add(run);
            }
        }

        var tournament = new Tournament
        {
            Name = "Winter Regionals",
            StartDate = today.AddDays(-24),
            EndDate = today.AddDays(-23),
        };
        db.Tournaments.Add(tournament);

        // "Game" is a magic name, not just any Ultimate session type — see
        // ActivitiesController.GameSessionTypeName. GamesListPage only lists activities
        // classified under that exact name.
        var ultimateTypes = await db.ActivitySessionTypes
            .Where(t => t.ActivityType == ActivityType.Ultimate)
            .ToListAsync();
        var gameType = ultimateTypes.FirstOrDefault(t => t.Name == "Game") ?? ultimateTypes.FirstOrDefault();
        for (var g = 0; g < 3; g++)
        {
            var gameDate = g < 2 ? tournament.StartDate : tournament.EndDate;
            var game = new Activity
            {
                Date = gameDate,
                Type = ActivityType.Ultimate,
                Source = ActivitySource.Manual,
                DurationSeconds = 4200 + gameRng.Next(-300, 300),
                DistanceKm = Math.Round(3m + (decimal)gameRng.NextDouble() * 2m, 2),
                Tournament = tournament,
                ActivitySessionTypeId = gameType?.Id,
                SessionTags = gameType is null
                    ? new()
                    : new() { new ActivitySessionTag { ActivitySessionTypeId = gameType.Id } },
                FinalScoreFor = 13 + gameRng.Next(0, 3),
                FinalScoreAgainst = 9 + gameRng.Next(0, 6),
                OnFieldSeconds = 2400,
                OffFieldSeconds = 1800,
                LivePlaySeconds = 1500,
                PointsPlayed = 12 + gameRng.Next(0, 4),
            };
            ApplyFakeGarminMetrics(game, 0.7 + gameRng.NextDouble() * 0.25, gameRng);
            db.Activities.Add(game);
        }

        // Non-Game Ultimate sessions spread across the window, so the monthly
        // Ultimate-distance trend and its per-type breakdown have more than one
        // type and more than one month. One is left without DistanceKm to
        // exercise the "logged without GPS" path (an indoor / hall session).
        var nonGameNames = new[] { "Club Training", "Pod", "Solo" };
        var nonGameTypes = nonGameNames
            .Select(n => ultimateTypes.FirstOrDefault(t => t.Name == n))
            .Where(t => t is not null)
            .ToList();
        if (nonGameTypes.Count > 0)
        {
            var n = 0;
            for (var d = windowStart.AddDays(4); d < today; d = d.AddDays(9), n++)
            {
                var stype = nonGameTypes[n % nonGameTypes.Count]!;
                var indoor = n % 5 == 2; // roughly one in five, deterministic
                var sess = new Activity
                {
                    Date = d,
                    Type = ActivityType.Ultimate,
                    Source = ActivitySource.Manual,
                    DurationSeconds = 3000 + ultimateRng.Next(-600, 900),
                    DistanceKm = indoor ? null : Math.Round(2m + (decimal)ultimateRng.NextDouble() * 5m, 2),
                    AvgHeartRate = 130 + ultimateRng.Next(-12, 18),
                    ActivitySessionTypeId = stype.Id,
                    SessionTags = new() { new ActivitySessionTag { ActivitySessionTypeId = stype.Id } },
                };
                ApplyFakeGarminMetrics(sess, indoor ? 0.2 : 0.4 + ultimateRng.NextDouble() * 0.4, ultimateRng);
                db.Activities.Add(sess);
            }
        }

        for (var d = windowStart; d < today; d = d.AddDays(1))
        {
            db.DailyWellness.Add(new DailyWellness
            {
                Date = d,
                SleepSeconds = 6 * 3600 + wellnessRng.Next(0, 7200),
                SleepScore = 55 + wellnessRng.Next(0, 40),
                HrvLastNightAvg = 45 + wellnessRng.Next(-10, 20),
                HrvWeeklyAvg = 50,
                HrvStatus = "Balanced",
                TrainingReadinessScore = 40 + wellnessRng.Next(0, 55),
                TrainingReadinessLevel = "Moderate",
                RestingHeartRate = 52 + wellnessRng.Next(-4, 6),
                BodyBatteryHigh = 80 + wellnessRng.Next(0, 15),
                BodyBatteryLow = 15 + wellnessRng.Next(0, 15),
                AvgStressLevel = 20 + wellnessRng.Next(0, 25),
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return today;
    }

    private static decimal BaseWeightFor(ExerciseCategory category, bool isAssisted)
    {
        if (isAssisted) return 20m;
        return category switch
        {
            ExerciseCategory.Push => 40m,
            ExerciseCategory.Pull => 35m,
            ExerciseCategory.Legs => 60m,
            ExerciseCategory.Core => 10m,
            ExerciseCategory.Cardio => 0m,
            _ => 20m,
        };
    }

    // TargetReps is a display string like "8-10" or "12" — pick a plausible logged
    // value within (or at) the prescribed range, the way a real set varies rep-to-rep.
    private static int ParseTargetReps(string targetReps, Random rng)
    {
        var parts = targetReps.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var lo) && int.TryParse(parts[1], out var hi) && hi >= lo)
            return rng.Next(lo, hi + 1);
        return int.TryParse(parts[0], out var single) ? single : 8;
    }
}
