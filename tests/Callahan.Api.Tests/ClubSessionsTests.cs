using System.Text.Json;
using Callahan.Api.Controllers;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Callahan.Api.Tests;

public class ClubSessionsTests
{
    private const int Pod = 6;
    private const int ClubTraining = 7;

    private static TrackSample S(double t, double spd) => new(t, 0, 0, spd);

    [Fact]
    public void FromTrack_CountsEffortsAndIntegratesOnlyAboveThreshold()
    {
        // Two sprints 20 s apart, each three 1 s samples at 6 m/s then a slow sample.
        var s = new List<TrackSample>
        {
            S(0, 2), S(1, 6), S(2, 6), S(3, 6), S(4, 2),
            S(20, 6), S(21, 6), S(22, 6), S(23, 2),
        };
        var f = ClubSessionMetrics.FromTrack(s);
        Assert.Equal(2, f.SprintCount);
        Assert.Equal(36, f.HighSpeedDistanceM); // 6 samples x 6 m/s x 1 s
    }

    [Fact]
    public void FromTrack_StutterAtTheThresholdIsOneEffort_AndLongGapsAddNoDistance()
    {
        var s = new List<TrackSample> { S(0, 6), S(1, 5), S(2, 6), S(3, 2), S(40, 6), S(60, 2) };
        var f = ClubSessionMetrics.FromTrack(s);
        Assert.Equal(2, f.SprintCount);          // 0-2 is one effort, 40 another
        Assert.Equal(12, f.HighSpeedDistanceM);  // 6 + 6; the 40 -> 60 s gap is skipped
    }

    [Fact]
    public void ParseRaw_ReadsMaxHrAndZones_AndRejectsAPartialZoneSet()
    {
        var full = ClubSessionMetrics.ParseRaw("""{"maxHR":195.0,"hrTimeInZone_1":4916.085,"hrTimeInZone_2":3140.81,"hrTimeInZone_3":1116.879,"hrTimeInZone_4":631.003,"hrTimeInZone_5":5.0}""");
        Assert.Equal(195, full.MaxHeartRate);
        Assert.Equal(new[] { 4916, 3141, 1117, 631, 5 }, full.ZoneSeconds);
        var partial = ClubSessionMetrics.ParseRaw("""{"maxHR":180,"hrTimeInZone_1":10}""");
        Assert.Equal(180, partial.MaxHeartRate);
        Assert.Null(partial.ZoneSeconds);
        Assert.Null(ClubSessionMetrics.ParseRaw("not json").MaxHeartRate);
    }

    private static async Task<int> Add(Callahan.Api.Data.AppDbContext db, int typeId, DateOnly date, string raw, bool track)
    {
        var a = new Activity
        {
            Date = date, Type = ActivityType.Ultimate, Source = ActivitySource.Garmin, DurationSeconds = 7200,
            DistanceKm = 6.2m, RawJson = raw, ActivitySessionTypeId = typeId,
        };
        a.SessionTags.Add(new ActivitySessionTag { ActivitySessionTypeId = typeId });
        if (track)
            a.Track = new ActivityTrack
            {
                StartEpochMs = 0, SampleCount = 3,
                SamplesJson = JsonSerializer.Serialize(new { t = new[] { 0, 1, 2 }, lat = new[] { 0.0, 0, 0 }, lon = new[] { 0.0, 0, 0 }, spd = new[] { 6.0, 6, 2 } }),
            };
        db.Activities.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task List_ReturnsOnlyClubTraining_NewestFirst_WithTrackFigures()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var older = await Add(db, ClubTraining, new DateOnly(2026, 11, 15), """{"maxHR":190}""", track: false);
        var newer = await Add(db, ClubTraining, new DateOnly(2026, 11, 22), """{"maxHR":195}""", track: true);
        await Add(db, Pod, new DateOnly(2026, 11, 20), "{}", track: true);

        var list = (List<Callahan.Api.DTOs.ClubSessionDto>)((OkObjectResult)(await new ClubSessionsController(db).GetAll()).Result!).Value!;
        Assert.Equal(new[] { newer, older }, list.Select(x => x.Id));
        Assert.Equal(12m, list[0].HighSpeedDistanceM);
        Assert.Equal(1, list[0].SprintCount);
        Assert.Equal(195, list[0].MaxHeartRate);
        Assert.Null(list[1].SprintCount); // no track synced

        var one = await new ClubSessionsController(db).Get(older);
        Assert.IsType<OkObjectResult>(one.Result);
        Assert.IsType<NotFoundResult>((await new ClubSessionsController(db).Get(99999)).Result);
    }
}
