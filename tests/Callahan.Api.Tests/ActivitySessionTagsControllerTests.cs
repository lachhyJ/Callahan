using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// PUT /api/activities/{id}/session-types replaces an activity's whole set of
// session-type labels. What matters: the primary is always also in the set,
// a wrong-ActivityType or unknown id is refused, clearing works, and a set
// with no primary is refused.
public class ActivitySessionTagsControllerTests
{
    // Seeded ids (AppDbContext.HasData): Ultimate Solo=4, Throws=5, Pod=6,
    // Club Training=7, Game=8; Running Easy Aerobic Run=3.
    private const int Throws = 5;
    private const int ClubTraining = 7;
    private const int Game = 8;
    private const int EasyRun = 3;

    private static AppDbContext NewDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<int> AddUltimateActivity(AppDbContext db)
    {
        var a = new Activity
        {
            Date = new DateOnly(2026, 9, 6),
            Type = ActivityType.Ultimate,
            Source = ActivitySource.Garmin,
            DurationSeconds = 3600,
        };
        db.Activities.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private static ActivityDto Ok(ActionResult<ActivityDto> result) =>
        Assert.IsType<ActivityDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task SetsPrimaryAndExtra_PrimaryFirst_AndAlwaysInTheSet()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        var dto = Ok(await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(ClubTraining, new List<int> { Throws })));

        Assert.Equal(ClubTraining, dto.ActivitySessionTypeId);
        Assert.Equal("Club Training", dto.ActivitySessionTypeName);
        Assert.Equal(new[] { "Club Training", "Throws" }, dto.SessionTypes!.Select(t => t.Name));
        Assert.Equal(2, await db.ActivitySessionTags.CountAsync(t => t.ActivityId == id));
    }

    [Fact]
    public async Task PrimaryNeedNotBeRepeatedInTypeIds()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        var dto = Ok(await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(ClubTraining, null)));

        Assert.Equal(ClubTraining, dto.ActivitySessionTypeId);
        Assert.Equal(new[] { "Club Training" }, dto.SessionTypes!.Select(t => t.Name));
    }

    [Fact]
    public async Task ReclassifyingDropsTagsNoLongerWanted()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(ClubTraining, new List<int> { Throws }));
        var dto = Ok(await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(Throws, new List<int>())));

        Assert.Equal(Throws, dto.ActivitySessionTypeId);
        Assert.Equal(new[] { "Throws" }, dto.SessionTypes!.Select(t => t.Name));
        Assert.Equal(1, await db.ActivitySessionTags.CountAsync(t => t.ActivityId == id));
    }

    [Fact]
    public async Task ClearingRemovesEveryTag()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(ClubTraining, new List<int> { Throws, Game }));
        var dto = Ok(await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(null, new List<int>())));

        Assert.Null(dto.ActivitySessionTypeId);
        Assert.Empty(dto.SessionTypes!);
        Assert.Equal(0, await db.ActivitySessionTags.CountAsync(t => t.ActivityId == id));
    }

    [Fact]
    public async Task RejectsExtrasWithNoPrimary()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        var result = await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(null, new List<int> { Throws }));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await db.ActivitySessionTags.CountAsync(t => t.ActivityId == id));
    }

    [Fact]
    public async Task RejectsASessionTypeFromTheWrongActivityType()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        var result = await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(EasyRun, null)); // a Running type on an Ultimate activity

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RejectsAnUnknownSessionType()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        var result = await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(9999, null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PromotingToGameComputesTheFieldSplitGate()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = NewDb(conn);
        var controller = new ActivitiesController(db);
        var id = await AddUltimateActivity(db);

        // Game + a second label. The Game analysis gate keys off the primary,
        // so a Game with no track just records the classifier version.
        var dto = Ok(await controller.UpdateSessionTypes(id,
            new UpdateActivitySessionTagsRequest(Game, new List<int> { Throws })));

        Assert.Equal(Game, dto.ActivitySessionTypeId);
        Assert.Contains(dto.SessionTypes!, t => t.Name == "Throws");
        Assert.NotNull(dto.LapClassifierVersion);
    }
}
