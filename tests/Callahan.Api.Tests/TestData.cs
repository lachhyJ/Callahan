using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Shared builders so the same setup isn't copied into every test file.
internal static class TestData
{
    // An AppDbContext over an in-memory SQLite database on `conn`, schema
    // created. The caller owns (and disposes) the connection and the context;
    // the database lives as long as the connection stays open.
    public static AppDbContext OpenDb(SqliteConnection conn)
    {
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    // One day of wellness, by metric name. Named arguments into the DTO rather
    // than 19 positional ones: most of its slots are int?, so a field inserted
    // into the record would otherwise still compile and quietly feed each test
    // the wrong metric.
    public static DailyWellnessDto Wellness(
        DateOnly date, int? readiness = null, int? sleepScore = null, int? sleepSeconds = null,
        int? hrv = null, int? rhr = null) => new(
            Id: 0, Date: date,
            SleepSeconds: sleepSeconds, DeepSleepSeconds: null, LightSleepSeconds: null, RemSleepSeconds: null, AwakeSeconds: null,
            SleepScore: sleepScore, SleepScoreQualifier: null,
            HrvLastNightAvg: hrv, HrvWeeklyAvg: null, HrvStatus: null,
            TrainingReadinessScore: readiness, TrainingReadinessLevel: null, TrainingReadinessFeedback: null,
            RestingHeartRate: rhr, BodyBatteryHigh: null, BodyBatteryLow: null, AvgStressLevel: null);
}
