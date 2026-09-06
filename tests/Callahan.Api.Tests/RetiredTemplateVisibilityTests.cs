using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// The program v3 rebuild replaced Day A/B/C with Gym 1/2/3. The old templates
// are retired rather than deleted, because 36 logged sessions reference them by
// WorkoutTemplateId and History, Streaks and the monthly reports all read their
// names through it.
//
// That makes two behaviours load-bearing and easy to break in opposite
// directions: the picker must NOT offer a retired template, and everything
// else must still be able to resolve one by Id. A filter added to the wrong
// query - or copied onto Start() for consistency - breaks history silently,
// since nothing throws; old sessions just start rendering without a name.
public class RetiredTemplateVisibilityTests
{
    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        // EnsureCreated seeds HasData, which still carries the original three.
        db.WorkoutTemplates.RemoveRange(db.WorkoutTemplates);
        db.SaveChanges();
        return db;
    }

    private static WorkoutTemplate Template(int id, string name, bool retired) => new()
    {
        Id = id,
        Name = name,
        Subtitle = "sub",
        SortOrder = id,
        IsRetired = retired
    };

    [Fact]
    public async Task GetAllHidesRetiredTemplatesFromThePicker()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        db.WorkoutTemplates.AddRange(
            Template(4, "Gym 1", retired: false),
            Template(1, "Day A (v1)", retired: true));
        db.SaveChanges();

        var result = await new WorkoutTemplatesController(db).GetAll();

        var templates = Assert.IsType<List<WorkoutTemplateSummaryDto>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["Gym 1"], templates.Select(t => t.Name));
    }

    [Fact]
    public async Task StartStillResolvesARetiredTemplateById()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        db.WorkoutTemplates.Add(Template(1, "Day A (v1)", retired: true));
        db.SaveChanges();

        var result = await new WorkoutTemplatesController(db).Start(1);

        var dto = Assert.IsType<WorkoutTemplateStartDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Day A (v1)", dto.TemplateName);
    }
}
