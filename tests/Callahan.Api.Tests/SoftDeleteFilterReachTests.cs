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
public class SoftDeleteFilterReachTests
{
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
}
