using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// A lift's measurement basis is a property of the exercise, derived from its
// full history, not from the window being viewed - otherwise the same lift
// reads as set volume in the monthly report and as e1RM on /trends. Seen live
// 2026-09-01.
public class LiftTrendsBasisTests
{
    [Fact]
    public async Task TrendsUseTheBasisOfTheFullHistory_NotTheWindow()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var exerciseId = db.Exercises.First().Id;

        void Session(DateOnly date, int sets, int reps, decimal kg)
        {
            var s = new WorkoutSession { Date = date };
            db.WorkoutSessions.Add(s);
            for (var i = 0; i < sets; i++)
                db.ExerciseSets.Add(new ExerciseSet { WorkoutSession = s, ExerciseId = exerciseId, Reps = reps, WeightKg = kg, SetOrder = i, SetType = SetType.Normal });
        }
        // Months ago, high-rep: the history's median is well over 12 reps.
        Session(new DateOnly(2026, 3, 10), 6, 20, 40m);
        // Inside the 2-month window, low-rep: on its own this reads as e1RM.
        Session(new DateOnly(2026, 8, 10), 2, 8, 50m);
        Session(new DateOnly(2026, 9, 10), 2, 8, 55m);
        db.SaveChanges();

        var controller = new TrendsController(db, FixedTimeProvider.Melbourne(new DateTime(2026, 9, 15, 12, 0, 0)));
        var trends = Assert.IsType<List<LiftTrendDto>>(Assert.IsType<OkObjectResult>((await controller.GetLiftTrends(2)).Result).Value);

        Assert.Equal(nameof(LiftBasis.SetVolume), Assert.Single(trends).Basis);
    }
}
