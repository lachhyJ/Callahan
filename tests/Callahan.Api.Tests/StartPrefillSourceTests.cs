using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Start() used to prefill from the template's single latest session, so an
// exercise skipped in that session came up blank even with older history.
public class StartPrefillSourceTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        db.WorkoutTemplates.RemoveRange(db.WorkoutTemplates);
        db.SaveChanges();
        return db;
    }

    private static ExerciseSet Set(int exerciseId, int order, int reps, decimal kg) =>
        new() { ExerciseId = exerciseId, SetOrder = order, Reps = reps, WeightKg = kg };

    [Fact]
    public async Task PrefillsFromTheLatestSessionThatLoggedEachExercise()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var a = db.Exercises.First();
        var b = db.Exercises.Skip(1).First();
        db.WorkoutTemplates.Add(new WorkoutTemplate
        {
            Id = 90, Name = "Gym X", Subtitle = "s", SortOrder = 90,
            Exercises =
            [
                new WorkoutTemplateExercise { ExerciseId = a.Id, ExerciseOrder = 0, TargetSets = 1, TargetReps = "8" },
                new WorkoutTemplateExercise { ExerciseId = b.Id, ExerciseOrder = 1, TargetSets = 1, TargetReps = "8" },
            ]
        });
        db.WorkoutSessions.AddRange(
            new WorkoutSession { Date = new DateOnly(2026, 9, 21), WorkoutTemplateId = 90, Sets = [Set(a.Id, 0, 8, 50), Set(b.Id, 0, 10, 20)] },
            new WorkoutSession { Date = new DateOnly(2026, 10, 5), WorkoutTemplateId = 90, Sets = [Set(a.Id, 0, 9, 55)] });
        db.SaveChanges();

        var result = await new WorkoutTemplatesController(db).Start(90);

        var dto = Assert.IsType<WorkoutTemplateStartDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var byId = dto.Exercises.ToDictionary(e => e.ExerciseId);
        Assert.Equal(55m, byId[a.Id].PreviousSets.Single().WeightKg);   // newest session wins
        Assert.Equal(20m, byId[b.Id].PreviousSets.Single().WeightKg);   // skipped there, falls back to 09-21
    }
}
