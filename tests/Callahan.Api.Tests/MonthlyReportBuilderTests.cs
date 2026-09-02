using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// End-to-end over a real (in-memory) SQLite database. The rules themselves are
// unit-tested in MonthlyReportMetricsTests; what this covers is the half that
// pure tests can't - that every query the builder issues actually translates
// and runs, including the lap-count projection and the template-target join
// behind the push/pull comparison.
public class MonthlyReportBuilderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    private static readonly DateOnly Aug = new(2026, 8, 1);

    public MonthlyReportBuilderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ActivitySessionType SessionType(string name, ActivityType type)
    {
        // EnsureCreated applies the seeded ActivitySessionTypes from HasData.
        return _db.ActivitySessionTypes.Single(t => t.Name == name && t.ActivityType == type);
    }

    private Activity AddActivity(DateOnly date, ActivityType type, string sessionTypeName,
        decimal? km = null, int seconds = 1800, decimal? highSpeedM = null, int activeLaps = 0)
    {
        var activity = new Activity
        {
            Date = date,
            Type = type,
            Source = ActivitySource.Manual,
            DurationSeconds = seconds,
            DistanceKm = km,
            HighSpeedDistanceM = highSpeedM,
            ActivitySessionTypeId = SessionType(sessionTypeName, type).Id,
        };
        _db.Activities.Add(activity);
        _db.SaveChanges();

        for (var i = 0; i < activeLaps; i++)
        {
            _db.ActivityLaps.Add(new ActivityLap
            {
                ActivityId = activity.Id,
                LapIndex = i,
                IntensityType = ActivityLap.ActiveIntensityType,
                DistanceM = 73m,
            });
        }
        // A rest lap that must not be counted as a work rep.
        if (activeLaps > 0)
        {
            _db.ActivityLaps.Add(new ActivityLap
            {
                ActivityId = activity.Id,
                LapIndex = activeLaps,
                IntensityType = "REST",
                DistanceM = 5m,
            });
        }
        _db.SaveChanges();
        return activity;
    }

    private Exercise AddExercise(string name, ExerciseCategory category)
    {
        var ex = new Exercise { Name = name, Category = category };
        _db.Exercises.Add(ex);
        _db.SaveChanges();
        return ex;
    }

    // A template prescribing TargetSets per exercise, plus `runs` sessions that
    // each logged `loggedSets[exerciseId]` working sets.
    private WorkoutTemplate AddTemplate(string name, params (Exercise Exercise, int TargetSets)[] slots)
    {
        var template = new WorkoutTemplate { Name = name, Subtitle = name };
        _db.WorkoutTemplates.Add(template);
        _db.SaveChanges();

        var order = 0;
        foreach (var (exercise, targetSets) in slots)
        {
            _db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
            {
                WorkoutTemplateId = template.Id,
                ExerciseId = exercise.Id,
                ExerciseOrder = order++,
                TargetSets = targetSets,
                TargetReps = "8-10",
                RestSeconds = 90,
            });
        }
        _db.SaveChanges();
        return template;
    }

    private void AddSession(DateOnly date, WorkoutTemplate? template, params (Exercise Exercise, int Sets)[] logged)
    {
        var session = new WorkoutSession { Date = date, WorkoutTemplateId = template?.Id };
        _db.WorkoutSessions.Add(session);
        _db.SaveChanges();

        var order = 0;
        foreach (var (exercise, sets) in logged)
        {
            for (var i = 0; i < sets; i++)
            {
                _db.ExerciseSets.Add(new ExerciseSet
                {
                    WorkoutSessionId = session.Id,
                    ExerciseId = exercise.Id,
                    Reps = 8,
                    WeightKg = 60m,
                    SetOrder = order++,
                    SetType = SetType.Normal,
                });
            }
        }
        _db.SaveChanges();
    }

    private Task<DTOs.MonthlyReportDto> Build() => new MonthlyReportBuilder(_db).BuildAsync(2026, 8);

    [Fact]
    public async Task UltimateBreaksDownByItsSessionTypes()
    {
        AddActivity(Aug.AddDays(2), ActivityType.Ultimate, "Club Training");
        AddActivity(Aug.AddDays(9), ActivityType.Ultimate, "Club Training");
        AddActivity(Aug.AddDays(10), ActivityType.Ultimate, "Game");
        AddActivity(Aug.AddDays(4), ActivityType.Ultimate, "Throws");

        var report = await Build();
        var ultimate = report.Consistency.SessionsByType
            .Where(t => t.Family == DTOs.SessionFamily.Ultimate)
            .ToDictionary(t => t.Label, t => t.Count);

        Assert.Equal(3, ultimate.Count);
        Assert.Equal(2, ultimate["Club Training"]);
        Assert.Equal(1, ultimate["Game"]);
        Assert.Equal(1, ultimate["Throws"]);
        // The old single flat "Ultimate" row is gone.
        Assert.DoesNotContain(report.Consistency.SessionsByType, t => t.Label == "Ultimate");
    }

    [Fact]
    public async Task RunningReportsRepsForIntervals_AndDistanceForEasyRuns()
    {
        AddActivity(Aug.AddDays(1), ActivityType.Running, "High Speed Intervals",
            km: 4.2m, highSpeedM: 1168m, activeLaps: 16);
        AddActivity(Aug.AddDays(6), ActivityType.Running, "Easy Aerobic Run", km: 8m, seconds: 2700);

        var report = await Build();

        var intervals = report.Running.ByType.Single(r => r.TypeName == "High Speed Intervals");
        Assert.Equal(16, intervals.WorkRepCount);          // the REST lap is excluded
        Assert.Equal(1.17m, intervals.HighSpeedDistanceKm);
        Assert.Null(intervals.TotalDistanceKm);
        Assert.Null(intervals.TotalDurationSeconds);

        var easy = report.Running.ByType.Single(r => r.TypeName == "Easy Aerobic Run");
        Assert.Equal(8m, easy.TotalDistanceKm);
        Assert.Equal(2700, easy.TotalDurationSeconds);
        Assert.Null(easy.WorkRepCount);
    }

    [Fact]
    public async Task Balance_FlagsPullsBeingSkippedAgainstWhatTheTemplatePrescribed()
    {
        var bench = AddExercise("Bench Press", ExerciseCategory.Push);
        var row = AddExercise("Barbell Row", ExerciseCategory.Pull);
        var template = AddTemplate("Workout 1", (bench, 4), (row, 4));

        // Four runs of the template: push done in full, pull consistently cut.
        for (var i = 0; i < 4; i++)
        {
            AddSession(Aug.AddDays(i * 3), template, (bench, 4), (row, 1));
        }

        var report = await Build();

        Assert.NotNull(report.Balance.FlaggedLine);
        Assert.StartsWith("Pull sets came in at 25% of plan", report.Balance.FlaggedLine);
        Assert.Contains("4 logged of 16 prescribed", report.Balance.FlaggedLine);
    }

    [Fact]
    public async Task Balance_SilentWhenAnAsymmetricProgramIsExecutedInFull()
    {
        var bench = AddExercise("Bench Press", ExerciseCategory.Push);
        var row = AddExercise("Barbell Row", ExerciseCategory.Pull);
        // Deliberately push-heavy: the old raw-ratio rule would have flagged
        // this every month.
        var template = AddTemplate("Workout 1", (bench, 8), (row, 3));

        AddSession(Aug.AddDays(1), template, (bench, 8), (row, 3));
        AddSession(Aug.AddDays(4), template, (bench, 8), (row, 3));

        var report = await Build();

        Assert.Null(report.Balance.FlaggedLine);
    }

    [Fact]
    public async Task Balance_IgnoresManualSessionsThatHaveNoPlanToCompareAgainst()
    {
        var bench = AddExercise("Bench Press", ExerciseCategory.Push);
        var row = AddExercise("Barbell Row", ExerciseCategory.Pull);
        AddSession(Aug.AddDays(1), template: null, (bench, 10), (row, 1));

        var report = await Build();

        Assert.Null(report.Balance.FlaggedLine);
    }

    [Fact]
    public async Task WindowSessionsIsReported_SoTheUiCanLabelTheMoversWindow()
    {
        var report = await Build();
        Assert.True(report.LoadProgression.WindowSessions > 0);
    }

    [Fact]
    public async Task WellnessOmitsReadiness_EvenWhenReadingsExist()
    {
        for (var i = 0; i < 28; i++)
        {
            _db.DailyWellness.Add(new DailyWellness
            {
                Date = Aug.AddDays(i),
                TrainingReadinessScore = 60,
                RestingHeartRate = 46,
                HrvLastNightAvg = 52,
                SleepSeconds = 25200,
            });
        }
        _db.SaveChanges();

        var report = await Build();

        Assert.NotNull(report.Wellness);
        Assert.DoesNotContain(report.Wellness!.Metrics, m => m.Key == "readiness");
        Assert.Equal("restingHeartRate", report.Wellness.Metrics[0].Key);
    }
}

// The snapshot-rebuild path. Locked reports store the whole DTO as JSON, so a
// change to the report's shape leaves old snapshots stale; rather than
// deleting rows (losing ViewedAt), a row below the current schema version is
// recomputed and overwritten in place.
public class MonthlyReportSnapshotRebuildTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MonthlyReportsController _controller;

    // Far enough in the past that the lock day has long passed.
    private const int Year = 2026;
    private const int Month = 1;

    public MonthlyReportSnapshotRebuildTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _controller = new MonthlyReportsController(_db, new MonthlyReportBuilder(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DTOs.MonthlyReportDto Unwrap(ActionResult<DTOs.MonthlyReportDto> result) =>
        (DTOs.MonthlyReportDto)((OkObjectResult)result.Result!).Value!;

    [Fact]
    public async Task StaleSnapshot_IsRebuiltInPlace_KeepingItsRowAndViewedAt()
    {
        var viewedAt = new DateTime(2026, 2, 14, 9, 0, 0, DateTimeKind.Utc);
        _db.MonthlyReports.Add(new MonthlyReport
        {
            Year = Year,
            Month = Month,
            // Shape from before this rework — deserialising it would not
            // produce today's report.
            ReportJson = """{"year":2026,"month":1,"headlineVerdict":"Steady month — old shape"}""",
            ComputedAt = new DateTime(2026, 2, 8, 0, 0, 0, DateTimeKind.Utc),
            SchemaVersion = 0,
            ViewedAt = viewedAt,
        });
        await _db.SaveChangesAsync();

        var dto = Unwrap(await _controller.Get(Year, Month));

        Assert.True(dto.IsLocked);
        Assert.Equal(viewedAt, dto.ViewedAt);

        var row = Assert.Single(_db.MonthlyReports);
        Assert.Equal(viewedAt, row.ViewedAt);
        // Must track MonthlyReportsController.CurrentReportSchemaVersion — the
        // assertion is "a stale row was upgraded to current", not "current is 1".
        // Bump this alongside it.
        Assert.Equal(2, row.SchemaVersion);
        Assert.DoesNotContain("old shape", row.ReportJson);
    }

    // Two readers both find no snapshot and both try to create one. This is
    // routine, not exotic: React runs effects twice in development, and the
    // first load after a schema-version bump has every month rebuilding at
    // once. (Year, Month) is unique, so the loser must take the winner's row
    // rather than 500.
    [Fact]
    public async Task ConcurrentFirstReads_DoNotCollideOnTheUniqueMonth()
    {
        var second = new MonthlyReportsController(_db, new MonthlyReportBuilder(_db));

        var first = Unwrap(await _controller.Get(Year, Month));

        // Simulate the racing request: it read "no row" before the winner
        // committed, so it still tries to insert.
        _db.ChangeTracker.Clear();
        var racer = new MonthlyReport
        {
            Year = Year,
            Month = Month,
            ReportJson = "{}",
            ComputedAt = DateTime.UtcNow,
        };
        _db.MonthlyReports.Add(racer);
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        _db.ChangeTracker.Clear();

        // The endpoint itself must still succeed and still see one row.
        var again = Unwrap(await second.Get(Year, Month));

        Assert.True(again.IsLocked);
        Assert.Equal(first.HeadlineVerdict, again.HeadlineVerdict);
        Assert.Single(_db.MonthlyReports);
    }

    [Fact]
    public async Task CurrentSnapshot_IsReturnedUnchanged_AndNotRecomputed()
    {
        // Prime a real snapshot at the current version.
        Unwrap(await _controller.Get(Year, Month));
        var stored = _db.MonthlyReports.Single();
        var computedAt = stored.ComputedAt;

        // A new session logged after the lock must not leak into the snapshot.
        _db.WorkoutSessions.Add(new WorkoutSession { Date = new DateOnly(Year, Month, 15) });
        await _db.SaveChangesAsync();

        var dto = Unwrap(await _controller.Get(Year, Month));

        Assert.Equal(0, dto.Consistency.TotalSessions);
        Assert.Equal(computedAt, _db.MonthlyReports.Single().ComputedAt);
    }
}
