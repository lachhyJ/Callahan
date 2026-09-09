using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// PUT /api/workouttemplates/{id}/layout rewrites slot order + superset links in
// one shot, as arranged in the active workout's Rearrange mode. The behaviour
// that matters: it applies the whole list atomically, it refuses a payload that
// doesn't describe exactly this template's slots (a partial or cross-template
// list is a client bug, not a partial reorder), and it never lets a "link to
// next" stick on the last slot by order.
public class WorkoutTemplateLayoutTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        // EnsureCreated seeds the real Gym 1/2/3 via HasData — clear it so each
        // test works with a template it fully controls.
        db.WorkoutTemplateExercises.RemoveRange(db.WorkoutTemplateExercises);
        db.WorkoutTemplates.RemoveRange(db.WorkoutTemplates);
        db.Exercises.RemoveRange(db.Exercises);
        db.SaveChanges();
        return db;
    }

    // A template with `count` slots, exercise ids and slot ids 1..count, orders
    // 0..count-1, no superset links. Returns the slot ids in order.
    private static List<int> SeedTemplate(AppDbContext db, int templateId, int count)
    {
        db.WorkoutTemplates.Add(new WorkoutTemplate
        {
            Id = templateId, Name = $"T{templateId}", Subtitle = "sub", SortOrder = templateId,
        });
        var slotIds = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var exId = templateId * 100 + i + 1;
            var slotId = templateId * 100 + i + 1;
            db.Exercises.Add(new Exercise { Id = exId, Name = $"Ex {exId}", Category = ExerciseCategory.Legs });
            db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
            {
                Id = slotId, WorkoutTemplateId = templateId, ExerciseId = exId,
                ExerciseOrder = i, TargetSets = 3, TargetReps = "8", RestSeconds = 90,
            });
            slotIds.Add(slotId);
        }
        db.SaveChanges();
        return slotIds;
    }

    [Fact]
    public async Task RewritesOrderAndSupersetFlags()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var slots = SeedTemplate(db, templateId: 1, count: 3);

        // Reverse the order, and link the first two of the new order as a superset.
        var result = await new WorkoutTemplatesController(db).UpdateLayout(1, new UpdateTemplateLayoutRequest(
        [
            new TemplateLayoutItemDto(slots[2], 0, SupersetWithNext: true),
            new TemplateLayoutItemDto(slots[1], 1, SupersetWithNext: true),
            new TemplateLayoutItemDto(slots[0], 2, SupersetWithNext: true),
        ]));

        Assert.IsType<NoContentResult>(result);

        var after = await db.WorkoutTemplateExercises
            .Where(te => te.WorkoutTemplateId == 1)
            .OrderBy(te => te.ExerciseOrder)
            .ToListAsync();
        Assert.Equal([slots[2], slots[1], slots[0]], after.Select(te => te.Id));
        // Trailing flag sanitised off; the earlier two kept.
        Assert.Equal([true, true, false], after.Select(te => te.SupersetWithNext));
    }

    [Fact]
    public async Task RejectsAPayloadMissingASlot()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var slots = SeedTemplate(db, templateId: 1, count: 3);

        var result = await new WorkoutTemplatesController(db).UpdateLayout(1, new UpdateTemplateLayoutRequest(
        [
            new TemplateLayoutItemDto(slots[0], 0, false),
            new TemplateLayoutItemDto(slots[1], 1, false),
        ]));

        Assert.IsType<BadRequestObjectResult>(result);
        // Nothing moved.
        var after = await db.WorkoutTemplateExercises.Where(te => te.WorkoutTemplateId == 1)
            .OrderBy(te => te.Id).ToListAsync();
        Assert.Equal([0, 1, 2], after.Select(te => te.ExerciseOrder));
    }

    [Fact]
    public async Task RejectsASlotFromAnotherTemplate()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var a = SeedTemplate(db, templateId: 1, count: 2);
        var b = SeedTemplate(db, templateId: 2, count: 2);

        var result = await new WorkoutTemplatesController(db).UpdateLayout(1, new UpdateTemplateLayoutRequest(
        [
            new TemplateLayoutItemDto(a[0], 0, false),
            new TemplateLayoutItemDto(b[1], 1, false),
        ]));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, await db.WorkoutTemplateExercises.CountAsync(te => te.Id == b[1] && te.ExerciseOrder == 1));
    }

    [Fact]
    public async Task UnknownTemplateIs404()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);

        var result = await new WorkoutTemplatesController(db).UpdateLayout(999, new UpdateTemplateLayoutRequest([]));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StartReturnsTheSupersetFlag()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var slots = SeedTemplate(db, templateId: 1, count: 3);
        db.WorkoutTemplateExercises.First(te => te.Id == slots[0]).SupersetWithNext = true;
        db.SaveChanges();

        var result = await new WorkoutTemplatesController(db).Start(1);

        var dto = Assert.IsType<WorkoutTemplateStartDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal([true, false, false], dto.Exercises.Select(e => e.SupersetWithNext));
    }
}
