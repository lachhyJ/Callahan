using Callahan.Api.Data;
using Callahan.Api.Models;
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
    public static async Task RunAsync(AppDbContext db)
    {
        db.ExerciseSets.RemoveRange(db.ExerciseSets);
        db.WorkoutSessions.RemoveRange(db.WorkoutSessions);
        db.ActivityLaps.RemoveRange(db.ActivityLaps);
        db.ActivityTracks.RemoveRange(db.ActivityTracks);
        db.Activities.RemoveRange(db.Activities);
        db.Tournaments.RemoveRange(db.Tournaments);
        db.DailyWellness.RemoveRange(db.DailyWellness);
        db.MonthlyReports.RemoveRange(db.MonthlyReports);
        db.Finishers.RemoveRange(db.Finishers);
        db.ExerciseNotes.RemoveRange(db.ExerciseNotes);
        await db.SaveChangesAsync();

        // Fixed seed: re-running this produces byte-for-byte the same fixture, so
        // regenerating it before a Playwright baseline refresh never itself shows up
        // as a diff.
        var rng = new Random(20260904);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var windowStart = today.AddDays(-70);

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

                var startedAt = date.ToDateTime(new TimeOnly(17, 30)).AddMinutes(rng.Next(-15, 15));
                var session = new WorkoutSession
                {
                    Date = date,
                    Name = template.Name,
                    WorkoutTemplateId = template.Id,
                    StartedAt = startedAt,
                    FinishedAt = startedAt.AddMinutes(50 + rng.Next(0, 25)),
                };

                foreach (var te in template.Exercises.OrderBy(e => e.ExerciseOrder))
                {
                    var baseWeight = BaseWeightFor(te.Exercise.Category, te.Exercise.IsAssisted);
                    var totalSets = te.WarmupSets + te.TargetSets;
                    for (var s = 0; s < totalSets; s++)
                    {
                        var isWarmup = s < te.WarmupSets;
                        var warmupFactor = isWarmup ? 0.5m + 0.15m * s : 1.0m;
                        var progressLoad = Math.Round((decimal)progress * 8m, 1);
                        var weight = te.Exercise.IsAssisted
                            ? -Math.Max(0m, baseWeight - progressLoad)
                            : Math.Round(baseWeight * warmupFactor + progressLoad, 1);

                        session.Sets.Add(new ExerciseSet
                        {
                            ExerciseId = te.ExerciseId,
                            SetOrder = s,
                            Reps = ParseTargetReps(te.TargetReps, rng),
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
            if (isRunDay && rng.NextDouble() < 0.7)
            {
                var distanceKm = Math.Round(4m + (decimal)rng.NextDouble() * 6m, 2);
                db.Activities.Add(new Activity
                {
                    Date = d,
                    Type = ActivityType.Running,
                    Source = ActivitySource.Manual,
                    DurationSeconds = (int)(distanceKm * 60m * (5m + (decimal)rng.NextDouble())),
                    DistanceKm = distanceKm,
                    Calories = (int)(distanceKm * 65m),
                    AvgHeartRate = 140 + rng.Next(-10, 15),
                    ActivitySessionTypeId = runTypes.Count > 0 ? runTypes[rng.Next(runTypes.Count)].Id : null,
                });
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
            db.Activities.Add(new Activity
            {
                Date = gameDate,
                Type = ActivityType.Ultimate,
                Source = ActivitySource.Manual,
                DurationSeconds = 4200 + rng.Next(-300, 300),
                Tournament = tournament,
                ActivitySessionTypeId = gameType?.Id,
                FinalScoreFor = 13 + rng.Next(0, 3),
                FinalScoreAgainst = 9 + rng.Next(0, 6),
                OnFieldSeconds = 2400,
                OffFieldSeconds = 1800,
                LivePlaySeconds = 1500,
                PointsPlayed = 12 + rng.Next(0, 4),
            });
        }

        for (var d = windowStart; d < today; d = d.AddDays(1))
        {
            db.DailyWellness.Add(new DailyWellness
            {
                Date = d,
                SleepSeconds = 6 * 3600 + rng.Next(0, 7200),
                SleepScore = 55 + rng.Next(0, 40),
                HrvLastNightAvg = 45 + rng.Next(-10, 20),
                HrvWeeklyAvg = 50,
                HrvStatus = "Balanced",
                TrainingReadinessScore = 40 + rng.Next(0, 55),
                TrainingReadinessLevel = "Moderate",
                RestingHeartRate = 52 + rng.Next(-4, 6),
                BodyBatteryHigh = 80 + rng.Next(0, 15),
                BodyBatteryLow = 15 + rng.Next(0, 15),
                AvgStressLevel = 20 + rng.Next(0, 25),
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
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
