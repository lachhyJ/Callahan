using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Program v4 has two gym layouts (Gym A/B in-season, Gym 1/2/3 with no club) and a
// mode the athlete flips between them. What matters: the mode defaults and persists,
// the picker offers only the current block's templates, and the things that used to
// assume one slot per exercise (prefill, "ready to progress") still answer sensibly
// when an exercise sits in both blocks.
public class TrainingModeTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;

    public TrainingModeTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        // EnsureCreated seeds content from HasData; these tests want templates they fully control.
        _db.WorkoutTemplateExercises.RemoveRange(_db.WorkoutTemplateExercises);
        _db.WorkoutTemplates.RemoveRange(_db.WorkoutTemplates);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private WorkoutTemplate AddTemplate(int id, string name, TemplateBlock block, int sortOrder) =>
        _db.WorkoutTemplates.Add(new WorkoutTemplate { Id = id, Name = name, Subtitle = "s", SortOrder = sortOrder, Block = block }).Entity;

    // ---- the mode -----------------------------------------------------------

    [Fact]
    public async Task TheModeDefaultsToNoClubAndPersistsOnceSet()
    {
        var controller = new TrainingModeController(_db);
        Assert.Equal("NoClub", Ok(await controller.Get()).Mode);

        Ok(await controller.Put(new TrainingModeDto("InSeason")));

        Assert.Equal("InSeason", Ok(await new TrainingModeController(_db).Get()).Mode);
    }

    [Theory]
    [InlineData("Any")]
    [InlineData("in-season")]
    [InlineData("")]
    [InlineData("99")]
    public async Task OnlyTheTwoRealModesAreAccepted(string value)
    {
        var result = await new TrainingModeController(_db).Put(new TrainingModeDto(value));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("NoClub", Ok(await new TrainingModeController(_db).Get()).Mode); // unchanged
    }

    // ---- the picker ---------------------------------------------------------

    [Fact]
    public async Task ThePickerOffersTheCurrentBlockAndAnythingNotTiedToOne()
    {
        AddTemplate(1, "Gym 1", TemplateBlock.NoClub, 1);
        AddTemplate(2, "Gym A", TemplateBlock.InSeason, 1);
        AddTemplate(3, "Custom", TemplateBlock.Any, 5);
        _db.SaveChanges();
        var controller = new WorkoutTemplatesController(_db);

        Assert.Equal(["Gym 1", "Custom"], Ok(await controller.GetAll()).Select(t => t.Name));

        await TrainingModeStore.SetAsync(_db, TemplateBlock.InSeason);
        Assert.Equal(["Gym A", "Custom"], Ok(await controller.GetAll()).Select(t => t.Name));
    }

    // ---- prefill ------------------------------------------------------------

    private Exercise AddExercise(int id) =>
        _db.Exercises.Add(new Exercise { Id = id, Name = $"Ex {id}", Category = ExerciseCategory.Legs }).Entity;

    private void AddSlot(int templateId, int exerciseId, int sets = 3, int repsMax = 8) =>
        _db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
        {
            WorkoutTemplateId = templateId, ExerciseId = exerciseId, ExerciseOrder = exerciseId,
            TargetSets = sets, TargetReps = repsMax.ToString(), TargetRepsMax = repsMax, RestSeconds = 90,
        });

    private void LogSession(int templateId, DateOnly date, int exerciseId, decimal kg, int reps, int sets = 3)
    {
        var session = new WorkoutSession { Date = date, WorkoutTemplateId = templateId };
        for (var i = 0; i < sets; i++)
            session.Sets.Add(new ExerciseSet { ExerciseId = exerciseId, SetOrder = i + 1, WeightKg = kg, Reps = reps, SetType = SetType.Normal });
        _db.WorkoutSessions.Add(session);
    }

    [Fact]
    public async Task ANewTemplateFallsBackToTheLatestSessionOfAnyTemplateThatLoggedTheExercise()
    {
        AddExercise(10);
        AddTemplate(1, "Gym 1", TemplateBlock.NoClub, 1);
        AddTemplate(2, "Gym A", TemplateBlock.InSeason, 1);
        AddSlot(1, 10);
        AddSlot(2, 10);
        LogSession(1, new DateOnly(2026, 11, 1), 10, kg: 100m, reps: 4);
        _db.SaveChanges();

        var start = Ok(await new WorkoutTemplatesController(_db).Start(2));

        var prefill = Assert.Single(start.Exercises).PreviousSets;
        Assert.Equal(3, prefill.Count);
        Assert.All(prefill, s => Assert.Equal(100m, s.WeightKg));
    }

    [Fact]
    public async Task ATemplatesOwnHistoryBeatsAMoreRecentSessionOfAnotherTemplate()
    {
        AddExercise(10);
        AddTemplate(1, "Gym 1", TemplateBlock.NoClub, 1);
        AddTemplate(2, "Gym A", TemplateBlock.InSeason, 1);
        AddSlot(1, 10);
        AddSlot(2, 10);
        LogSession(2, new DateOnly(2026, 10, 1), 10, kg: 90m, reps: 4);   // Gym A, older
        LogSession(1, new DateOnly(2026, 11, 1), 10, kg: 100m, reps: 4);  // Gym 1, newer
        _db.SaveChanges();

        var start = Ok(await new WorkoutTemplatesController(_db).Start(2));

        Assert.All(Assert.Single(start.Exercises).PreviousSets, s => Assert.Equal(90m, s.WeightKg));
    }

    [Fact]
    public async Task AnExerciseNobodyHasLoggedStaysBlank()
    {
        AddExercise(10);
        AddTemplate(2, "Gym A", TemplateBlock.InSeason, 1);
        AddSlot(2, 10);
        _db.SaveChanges();

        var start = Ok(await new WorkoutTemplatesController(_db).Start(2));

        Assert.Empty(Assert.Single(start.Exercises).PreviousSets);
    }

    // ---- ready to progress --------------------------------------------------

    [Fact]
    public async Task ReadyToProgressUsesTheCurrentBlocksSlotNotWhicheverSortsFirst()
    {
        AddExercise(10);
        // Gym A sorts first (and asks for 3x8), Gym 1 asks for 4x5. Four sets of five is
        // enough for Gym 1's criteria and not for Gym A's, so which slot is read decides it.
        AddTemplate(1, "Gym A", TemplateBlock.InSeason, 1);
        AddTemplate(2, "Gym 1", TemplateBlock.NoClub, 2);
        AddSlot(1, 10, sets: 3, repsMax: 8);
        AddSlot(2, 10, sets: 4, repsMax: 5);
        LogSession(2, new DateOnly(2026, 11, 1), 10, kg: 100m, reps: 5, sets: 4);
        _db.SaveChanges();
        var controller = new ExercisesController(_db);

        // NoClub (the default): Gym 1's slot, which the last session satisfied.
        var noClub = Ok(await controller.GetStats(10));
        Assert.NotNull(noClub.ProgressionReadiness);
        Assert.Equal(5, noClub.ProgressionReadiness!.TargetRepsMax);

        // In-season: Gym A's slot, which it did not (no Gym A session yet, so it reads the
        // latest session of any template, and four sets of five do not reach 3x8).
        await TrainingModeStore.SetAsync(_db, TemplateBlock.InSeason);
        Assert.Null(Ok(await controller.GetStats(10)).ProgressionReadiness);
    }
}
