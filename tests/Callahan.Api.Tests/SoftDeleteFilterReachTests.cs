using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Characterisation test, not a bug reproduction. EF logs a startup warning
// that WorkoutSession has a global query filter while being the required end
// of its relationship with ExerciseSet — "may lead to unexpected results when
// the required entity is filtered out". Every volume figure in the app is
// computed by querying ExerciseSets and reaching WorkoutSession, so if the
// soft-delete filter did NOT carry across, deleted sessions would silently
// inflate volume, trends and reports.
//
// Checked 2026-09-01: it does carry, in both query shapes the app uses. This
// test exists to catch that changing.
//
// 2026-09-02: EF raises the identical warning for Activity against ActivityLap
// and ActivityTrack, and those were unpinned. It matters for the same reason —
// TrendsController and MonthlyReportBuilder both count work reps by rooting on
// ActivityLaps and reaching Activity, so a filter that failed to carry would
// let deleted activities inflate rep counts in trends and monthly reports.
public class SoftDeleteFilterReachTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SoftDeletedSessionsAreExcludedFromSetsQueriedViaTheNavigation()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();

        var ex = new Exercise { Name = "Probe", Category = ExerciseCategory.Push };
        db.Exercises.Add(ex);
        db.SaveChanges();

        var live = new WorkoutSession { Date = new DateOnly(2026, 8, 1) };
        var dead = new WorkoutSession { Date = new DateOnly(2026, 8, 2), DeletedAt = DateTime.UtcNow };
        db.WorkoutSessions.AddRange(live, dead);
        db.SaveChanges();

        foreach (var s in new[] { live, dead })
            db.ExerciseSets.Add(new ExerciseSet { WorkoutSessionId = s.Id, ExerciseId = ex.Id, Reps = 8, WeightKg = 100m, SetOrder = 0 });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        // The pattern used by every volume query in the app.
        var viaNavigation = await db.ExerciseSets
            .Where(s => s.WorkoutSession.Date >= new DateOnly(2026, 8, 1))
            .CountAsync();

        var fromSessionsRoot = await db.WorkoutSessions.SelectMany(s => s.Sets).CountAsync();

        // The other shape, which is what the EF startup warning is actually
        // about: a query rooted on ExerciseSet whose predicate does NOT touch
        // the navigation, then Includes the filtered principal.
        var withoutNavigationPredicate = await db.ExerciseSets
            .Where(s => s.SetType != SetType.Warmup)
            .Include(s => s.WorkoutSession)
            .ToListAsync();

        var orphaned = withoutNavigationPredicate.Count(s => s.WorkoutSession is null);

        Assert.Equal(1, fromSessionsRoot);   // filter definitely applies from the filtered root
        Assert.Equal(1, viaNavigation);      // does it apply through the navigation?
        Assert.Single(withoutNavigationPredicate);          // and without one?
        Assert.Equal(0, orphaned);           // no set left holding a null session
    }

    [Fact]
    public async Task SoftDeletedActivitiesAreExcludedFromLapsQueriedViaTheNavigation()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        var live = new Activity { Date = new DateOnly(2026, 8, 1), Type = ActivityType.Running };
        var dead = new Activity { Date = new DateOnly(2026, 8, 2), Type = ActivityType.Running, DeletedAt = DateTime.UtcNow };
        db.Activities.AddRange(live, dead);
        db.SaveChanges();

        foreach (var a in new[] { live, dead })
            db.ActivityLaps.Add(new ActivityLap
            {
                ActivityId = a.Id,
                LapIndex = 0,
                IntensityType = ActivityLap.ActiveIntensityType,
            });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        // The exact shape TrendsController and MonthlyReportBuilder use for
        // work-rep counts: rooted on laps, predicate reaching the principal.
        var activeLapCounts = await db.ActivityLaps
            .Where(l => l.IntensityType == ActivityLap.ActiveIntensityType
                     && l.Activity.Type == ActivityType.Running
                     && l.Activity.Date >= new DateOnly(2026, 8, 1))
            .GroupBy(l => l.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

        var fromActivitiesRoot = await db.Activities.SelectMany(a => a.Laps).CountAsync();

        // And the shape the warning is really about: rooted on the dependent
        // with no predicate touching the navigation.
        var withoutNavigationPredicate = await db.ActivityLaps
            .Where(l => l.LapIndex == 0)
            .Include(l => l.Activity)
            .ToListAsync();

        Assert.Equal(1, fromActivitiesRoot);
        Assert.Single(activeLapCounts);
        Assert.Equal(live.Id, activeLapCounts.Keys.Single());
        Assert.Single(withoutNavigationPredicate);
        Assert.Equal(0, withoutNavigationPredicate.Count(l => l.Activity is null));
    }

    [Fact]
    public async Task SoftDeletedActivitiesAreExcludedFromTracksQueriedViaTheNavigation()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        var live = new Activity { Date = new DateOnly(2026, 8, 1), Type = ActivityType.Ultimate };
        var dead = new Activity { Date = new DateOnly(2026, 8, 2), Type = ActivityType.Ultimate, DeletedAt = DateTime.UtcNow };
        db.Activities.AddRange(live, dead);
        db.SaveChanges();

        foreach (var a in new[] { live, dead })
            db.ActivityTracks.Add(new ActivityTrack { ActivityId = a.Id, StartEpochMs = 0, SampleCount = 1, SamplesJson = "{}" });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var viaNavigation = await db.ActivityTracks.Where(t => t.Activity.Type == ActivityType.Ultimate).CountAsync();
        var withoutNavigationPredicate = await db.ActivityTracks.Include(t => t.Activity).ToListAsync();

        Assert.Equal(1, viaNavigation);
        Assert.Single(withoutNavigationPredicate);
        Assert.Equal(0, withoutNavigationPredicate.Count(t => t.Activity is null));
    }
}
