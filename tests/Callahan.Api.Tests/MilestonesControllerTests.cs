using Callahan.Api.Controllers;
using Callahan.Api.Services;
using Callahan.Api.Services.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Tests;

// The Dashboard's "next review / test day" line reads the Training calendar. These pin
// which titles count, the lookahead, and that a missing or broken calendar is silence
// rather than an error.
public class MilestonesControllerTests
{
    // Tuesday 10 Nov 2026, 9am Melbourne.
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static TimeProvider Clock() => FixedTimeProvider.Melbourne(new DateTime(2026, 11, 10, 9, 0, 0));

    private static CalendarEvent Ev(string title, int daysFromToday) =>
        new($"uid-{title}-{daysFromToday}", title, Today.AddDays(daysFromToday), null);

    private sealed class FakeCalendar(IReadOnlyList<CalendarEvent> events, bool configured = true, bool down = false) : ICalendarClient
    {
        public bool IsConfigured => configured;
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
            down ? throw new CalendarUnavailableException("down") : Task.FromResult(events);
        public Task CreateEventAsync(string title, DateOnly day, TimeOfDay part, CancellationToken ct) => throw new NotSupportedException();
        public Task MoveEventAsync(CalendarEvent existing, DateOnly day, TimeOfDay part, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteEventAsync(CalendarEvent existing, CancellationToken ct) => throw new NotSupportedException();
    }

    private static async Task<MilestoneDto?> Next(ICalendarClient? calendar)
    {
        var result = await new MilestonesController(Clock(), calendar).Next();
        return result.Result is OkObjectResult ok ? Assert.IsType<MilestoneDto>(ok.Value) : null;
    }

    [Fact]
    public async Task TheEarliestMilestoneWinsAndOtherEventsAreIgnored()
    {
        var next = await Next(new FakeCalendar([
            Ev("Gym 1", 0),
            Ev("Field 1", 1),
            Ev("Program review", 20),
            Ev("T0 testing", 13),
            Ev("Tournament prep", 2), // starts with T, but not T and a digit
        ]));

        Assert.Equal(new MilestoneDto("T0 testing", Today.AddDays(13)), next);
    }

    [Theory]
    [InlineData("T0 testing", true)]
    [InlineData("t2 testing + program review", true)]
    [InlineData("Program review (post-Nationals)", true)]
    [InlineData("T4", true)]
    [InlineData("T5 testing", false)]
    [InlineData("Tryout", false)]
    [InlineData("Review footage", false)]
    public void WhichTitlesCount(string title, bool counts) =>
        Assert.Equal(counts, ProgramMilestones.IsMilestone(title));

    [Fact]
    public async Task TodayCountsAndBeyondTheLookaheadDoesNot()
    {
        Assert.Equal("T1 testing", (await Next(new FakeCalendar([Ev("T1 testing", 0)])))?.Title);
        Assert.Null(await Next(new FakeCalendar([Ev("T1 testing", ProgramMilestones.LookaheadDays + 1)])));
        Assert.Null(await Next(new FakeCalendar([Ev("T1 testing", -1)])));
    }

    [Fact]
    public async Task NoCalendarOrADownCalendarIsSilent()
    {
        Assert.Null(await Next(null));
        Assert.Null(await Next(new FakeCalendar([Ev("T0 testing", 1)], configured: false)));
        Assert.Null(await Next(new FakeCalendar([Ev("T0 testing", 1)], down: true)));
    }
}
