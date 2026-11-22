using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Ultimate activities are tagged at sync: title keywords first, the usual club
// time slot only when the title matches nothing, and never on a re-sync.
public class UltimateAutoClassifyTests
{
    // 2026-09-06 is a Sunday, 2026-09-09 a Wednesday.
    private static string Blob(string startLocal) => $$"""{"activityId":1,"startTimeLocal":"{{startLocal}}"}""";

    private static CreateActivityRequest Req(string? title, string startLocal, int seconds, DateOnly? date = null, string id = "1") => new(
        Date: date ?? new DateOnly(2026, 9, 6), Type: "Ultimate", DurationSeconds: seconds,
        DistanceKm: null, Calories: null, AvgHeartRate: null, Notes: title,
        Source: "Garmin", GarminActivityId: id, RawJson: Blob(startLocal));

    private static async Task<string?> Tagged(AppDbContext db, CreateActivityRequest req)
    {
        await new ActivitiesController(db).Create(req);
        var a = await db.Activities.Include(x => x.ActivitySessionType).Include(x => x.SessionTags)
            .SingleAsync(x => x.GarminActivityId == req.GarminActivityId);
        if (a.ActivitySessionType is not null) Assert.Single(a.SessionTags);
        return a.ActivitySessionType?.Name;
    }

    [Fact]
    public async Task TitleKeywordTagsAtSync_EvenInsideTheClubSlot()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        // A Throws session on a Wednesday evening is still Throws.
        Assert.Equal("Throws", await Tagged(db, Req("Throws- Solo", "2026-09-09 19:00:00", 7200, new DateOnly(2026, 9, 9))));
    }

    [Fact]
    public async Task ChillyTrainingTitle_IsClubTraining_ButBareTrainingIsNot()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        Assert.Equal("Club Training", await Tagged(db, Req("Chilly Training", "2026-09-08 10:00:00", 1800, id: "1")));
        Assert.Null(await Tagged(db, Req("Training", "2026-09-08 10:00:00", 1800, id: "2")));
    }

    [Theory]
    [InlineData("2026-09-09 19:10:00", 7000, true)]   // Wednesday evening, about 2 h
    [InlineData("2026-09-09 19:00:00", 3600, false)]  // too short
    [InlineData("2026-09-09 13:00:00", 7200, false)]  // wrong time of day
    public async Task TimeSlotFallback_Wednesday(string start, int seconds, bool expectClub)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var name = await Tagged(db, Req("Ultimate", start, seconds, new DateOnly(2026, 9, 9)));
        Assert.Equal(expectClub ? "Club Training" : null, name);
    }

    [Theory]
    [InlineData("2026-09-06 12:05:00", 10800, true)]
    [InlineData("2026-09-06 15:00:00", 10200, true)]
    [InlineData("2026-09-06 09:00:00", 10800, false)]
    [InlineData("2026-09-07 12:00:00", 10800, false)]  // Monday
    public void TimeSlotFallback_Sunday(string start, int seconds, bool expectClub) =>
        Assert.Equal(expectClub, UltimateSessionClassifier.FitsClubSlot(DateTime.Parse(start), seconds));

    [Fact]
    public async Task TournamentWeekend_SkipsTheTimeSlotFallback()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        db.Tournaments.Add(new Tournament { Name = "Regionals", StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2026, 9, 6) });
        await db.SaveChangesAsync();
        Assert.Null(await Tagged(db, Req("Ultimate", "2026-09-06 12:00:00", 10800)));
    }

    [Fact]
    public async Task ReSync_NeverOverwritesAManualTagOrClear()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var controller = new ActivitiesController(db);
        await controller.Create(Req("Pod", "2026-09-06 12:00:00", 10800));
        var a = await db.Activities.SingleAsync();
        // Manually clear the tag, then re-sync with a title that would tag it.
        await controller.UpdateSessionTypes(a.Id, new UpdateActivitySessionTagsRequest(null, new List<int>()));
        await controller.Create(Req("Pod", "2026-09-06 12:00:00", 10800));
        Assert.Null((await db.Activities.SingleAsync()).ActivitySessionTypeId);
    }
}

// The picker's suggestion (frontend) and the sync-time classifier (backend)
// read the same title rules; this fails the build if the two lists drift.
public class UltimateTitleKeywordParityTests
{
    [Fact]
    public void FrontendSuggestionKeywords_MatchTheBackendList()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "src", "utils", "ultimateSessionSuggestion.js")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var js = File.ReadAllText(Path.Combine(dir!.FullName, "frontend", "src", "utils", "ultimateSessionSuggestion.js"));
        var frontend = System.Text.RegularExpressions.Regex
            .Matches(js, @"\{ keyword: '([^']+)', name: '([^']+)' \}")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
            .ToList();
        Assert.Equal(UltimateSessionClassifier.TitleKeywords.ToList(), frontend);
    }
}
