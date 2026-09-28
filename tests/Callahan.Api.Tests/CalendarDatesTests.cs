using Callahan.Api.Controllers;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

public class CalendarDatesTests
{
    [Theory]
    [InlineData(2026, 9, 7, 2026, 9, 7)]    // Monday is its own week start
    [InlineData(2026, 9, 13, 2026, 9, 7)]   // Sunday belongs to the week before
    [InlineData(2026, 10, 1, 2026, 9, 28)]  // across a month boundary
    public void MondayOf(int y, int m, int d, int ey, int em, int ed) =>
        Assert.Equal(new DateOnly(ey, em, ed), CalendarDates.MondayOf(new DateOnly(y, m, d)));

    [Fact]
    public void WindowStart_CountsTheCurrentMonth()
    {
        Assert.Equal(new DateOnly(2026, 7, 1), CalendarDates.WindowStart(new DateOnly(2026, 9, 15), 3));
        Assert.Equal(new DateOnly(2026, 9, 1), CalendarDates.WindowStart(new DateOnly(2026, 9, 15), 1));
        Assert.Equal(new DateOnly(2025, 11, 1), CalendarDates.WindowStart(new DateOnly(2026, 1, 3), 3));
    }
}

// Endpoints that take a months window used to throw (KeyNotFoundException -> 500)
// for months <= 0; they now clamp like the season and Ultimate endpoints do.
public class TrendsMonthsClampTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ANonPositiveWindowIsTreatedAsOneMonth(int months)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var controller = new TrendsController(db, FixedTimeProvider.Melbourne(new DateTime(2026, 9, 15, 12, 0, 0)));

        var points = Assert.IsType<List<TrendPointDto>>(Assert.IsType<OkObjectResult>((await controller.GetTrends(months)).Result).Value);
        Assert.Single(points);
        Assert.IsType<OkObjectResult>((await controller.GetLiftTrends(months)).Result);
        Assert.IsType<OkObjectResult>((await controller.GetRunTypeTrends(months)).Result);
    }
}

// Between midnight and 3am the day in progress is still the previous day's
// training day, as it is for routines and the frontend. A session started at
// 00:10 is saved against Monday; until it's saved, Monday's slot mustn't
// already read as missed.
public class PlanTrainingDayTests
{
    [Fact]
    public async Task At0030Tuesday_MondaysUnloggedSlotIsNotYetMissed()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        db.PlanSlots.Add(new PlanSlot { DefaultDayOfWeek = 0, SlotOrder = 0, Kind = PlanSlotKind.Gym, Label = "Gym 1", WorkoutTemplateId = 1 });
        db.SaveChanges();

        var clock = FixedTimeProvider.Melbourne(new DateTime(2026, 9, 8, 0, 30, 0)); // Tuesday 00:30
        var week = Assert.IsType<WeekPlanDto>(Assert.IsType<OkObjectResult>((await new PlanController(db, clock).Get(null)).Result).Value);

        var monday = week.Days.Single(d => d.DayOfWeek == 0);
        Assert.Equal(new DateOnly(2026, 9, 7), monday.Date);
        Assert.NotEqual("Missed", monday.Slots.Single().State);
    }
}
