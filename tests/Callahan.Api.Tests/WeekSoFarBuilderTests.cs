using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Callahan.Api.Services;
using static Callahan.Api.Services.WeekSoFarBuilder;

namespace Callahan.Api.Tests;

// The "this week" widget's rules, on hand-built weeks: what fills a program
// session, which logs only show, and what is left. The builder is pure, so none
// of this needs a database. Slots are the program's real non-optional Gym and
// Field slots, in its order; ids mirror the seed (Field 1 = type 9, Pod = 6, ...).
public class WeekSoFarBuilderTests
{
    private static readonly DateOnly Monday = new(2026, 9, 28);
    private static readonly DateOnly Sunday = new(2026, 10, 4);

    private static readonly SlotInput[] Program =
    [
        new(PlanSlotKind.Field, "Field 1 - Acceleration & Jumps", null, 9),
        new(PlanSlotKind.Gym, "Gym 1", 4, null),
        new(PlanSlotKind.Gym, "Gym 2", 5, null),
        new(PlanSlotKind.Field, "Field 2 - Repeat Effort & COD", null, 10),
        new(PlanSlotKind.Gym, "Gym 3", 6, null),
    ];

    private const int Solo = 4, Throws = 5, Pod = 6, ClubTraining = 7, Game = 8, Field1 = 9, Field2 = 10, EasyRun = 3;

    private static DateOnly Day(int offset) => Monday.AddDays(offset);

    private static GymLog Gym(int day, int? template, string? name) => new(Day(day), template, name);

    private static ActivityLog Field(int day, int typeId, string name, SessionTypeFamily family = SessionTypeFamily.Field) =>
        new(Day(day), ActivityType.Ultimate, typeId, name, family);

    private static ActivityLog Ultimate(int day, int typeId, string name) =>
        Field(day, typeId, name, SessionTypeFamily.Ultimate);

    private static WidgetWeekDto Build(IReadOnlyList<GymLog>? gym = null, IReadOnlyList<ActivityLog>? activities = null) =>
        WeekSoFarBuilder.Build(Monday, Sunday, Program, gym ?? [], activities ?? []);

    private static string[] Chips(WidgetWeekDto w, int day) => w.Days[day].Chips.Select(c => c.Short).ToArray();

    [Fact]
    public void AnEmptyWeekIsEverythingStillToDoInProgramOrder()
    {
        var w = Build();

        Assert.Equal(new WidgetTallyDto(0, 3), w.Gym);
        Assert.Equal(new WidgetTallyDto(0, 2), w.Field);
        Assert.Equal(["Field 1", "Gym 1", "Gym 2", "Field 2", "Gym 3"], w.Left);
        Assert.Equal(7, w.Days.Count);
        Assert.All(w.Days, d => Assert.Empty(d.Chips));
        Assert.Equal(Monday, w.WeekStart);
        Assert.Equal(Sunday, w.TrainingDay);
    }

    [Fact]
    public void AGymSessionCountsOnAnyDayOfTheWeek()
    {
        // Gym 1 on a Monday although the program puts it on a Tuesday.
        var w = Build(gym: [Gym(0, 4, "Gym 1")]);

        Assert.Equal(new WidgetTallyDto(1, 3), w.Gym);
        Assert.DoesNotContain("Gym 1", w.Left);
        var chip = Assert.Single(w.Days[0].Chips);
        Assert.Equal("G1", chip.Short);
        Assert.True(chip.Counted);
    }

    [Fact]
    public void LoggingTheSameGymSessionTwiceFillsOneSlotAndTheSecondOnlyShows()
    {
        var w = Build(gym: [Gym(0, 4, "Gym 1"), Gym(3, 4, "Gym 1")]);

        Assert.Equal(1, w.Gym.Done);
        Assert.True(w.Days[0].Chips[0].Counted);
        Assert.False(w.Days[3].Chips[0].Counted);
    }

    [Fact]
    public void ACustomWorkoutShowsAsGymButFillsNothing()
    {
        var w = Build(gym: [Gym(1, null, null), Gym(2, 1, "Day A")]);

        Assert.Equal(0, w.Gym.Done);
        Assert.Equal(["Gym"], Chips(w, 1));
        Assert.Equal(["Gym"], Chips(w, 2));
        Assert.All(w.Days.SelectMany(d => d.Chips), c => Assert.False(c.Counted));
    }

    [Fact]
    public void AFieldSessionFillsItsOwnSlot()
    {
        var w = Build(activities: [Field(1, Field1, "Field 1 - Acceleration & Jump Quality")]);

        Assert.Equal(new WidgetTallyDto(1, 2), w.Field);
        Assert.Equal(["Gym 1", "Gym 2", "Field 2", "Gym 3"], w.Left);
        Assert.Equal(["F1"], Chips(w, 1));
    }

    // The case that prompted this widget: Field 1, then Pod and Solo. Pod stands in
    // for the Field 2 that was not logged, and Solo is one more than the week needs.
    [Fact]
    public void PodStandsInForAFieldSlotAndAnExtraSoloOnlyShows()
    {
        var w = Build(activities:
        [
            Field(1, Field1, "Field 1 - Acceleration & Jump Quality"),
            Ultimate(2, Pod, "Pod"),
            Ultimate(4, Solo, "Solo"),
        ]);

        Assert.Equal(new WidgetTallyDto(2, 2), w.Field);
        Assert.DoesNotContain("Field 2", w.Left);
        Assert.True(w.Days[1].Chips[0].Counted);
        Assert.True(w.Days[2].Chips[0].Counted);
        Assert.False(w.Days[4].Chips[0].Counted);
        Assert.Equal(["F1"], Chips(w, 1));
        Assert.Equal(["Pod"], Chips(w, 2));
        Assert.Equal(["Solo"], Chips(w, 4));
    }

    [Fact]
    public void AnExactFieldMatchTakesItsSlotEvenWhenAPodCameFirst()
    {
        // Pod on Tuesday, Field 1 on Wednesday: Field 1 fills its own slot, and Pod
        // takes the other one rather than competing for it.
        var w = Build(activities: [Ultimate(1, Pod, "Pod"), Field(2, Field1, "Field 1 - Acceleration & Jump Quality")]);

        Assert.Equal(2, w.Field.Done);
        Assert.Empty(w.Left.Where(l => l.StartsWith("Field")));
    }

    [Theory]
    [InlineData(ClubTraining, "Club Training", "Club")]
    [InlineData(Game, "Game", "Game")]
    [InlineData(Throws, "Throws", "Throw")]
    public void OtherUltimateSessionsShowButDoNotCountTowardField(int typeId, string name, string chip)
    {
        var w = Build(activities: [Ultimate(2, typeId, name)]);

        Assert.Equal(0, w.Field.Done);
        Assert.Equal([chip], Chips(w, 2));
        Assert.False(w.Days[2].Chips[0].Counted);
    }

    [Fact]
    public void ARunShowsAsRunAndDoesNotCount()
    {
        var w = Build(activities:
        [
            new ActivityLog(Day(6), ActivityType.Running, null, null, null),
            new ActivityLog(Day(5), ActivityType.Running, EasyRun, "Easy Aerobic Run", SessionTypeFamily.Run),
        ]);

        Assert.Equal(0, w.Field.Done);
        Assert.Equal(["Run"], Chips(w, 6));
        Assert.Equal(["Run"], Chips(w, 5));
    }

    [Fact]
    public void AnUltimateActivityWithNoTypeShowsAsFieldButDoesNotCount()
    {
        var w = Build(activities: [new ActivityLog(Day(3), ActivityType.Ultimate, null, null, null)]);

        Assert.Equal(["Field"], Chips(w, 3));
        Assert.Equal(0, w.Field.Done);
    }

    [Fact]
    public void ADayShowsGymBeforeActivities()
    {
        var w = Build(
            gym: [Gym(4, 6, "Gym 3")],
            activities: [Ultimate(4, Solo, "Solo")]);

        Assert.Equal(["G3", "Solo"], Chips(w, 4));
    }

    [Fact]
    public void LogsOutsideTheWeekAreIgnored()
    {
        var w = Build(
            gym: [new GymLog(Monday.AddDays(-1), 4, "Gym 1"), new GymLog(Monday.AddDays(7), 5, "Gym 2")],
            activities: [new ActivityLog(Monday.AddDays(-1), ActivityType.Running, null, null, null)]);

        Assert.Equal(0, w.Gym.Done);
        Assert.All(w.Days, d => Assert.Empty(d.Chips));
    }

    [Fact]
    public void AWeekWithEverythingDoneHasNothingLeft()
    {
        var w = Build(
            gym: [Gym(1, 4, "Gym 1"), Gym(3, 5, "Gym 2"), Gym(5, 6, "Gym 3")],
            activities:
            [
                Field(0, Field1, "Field 1 - Acceleration & Jump Quality"),
                Field(4, Field2, "Field 2 - Repeat Effort & COD"),
            ]);

        Assert.Equal(new WidgetTallyDto(3, 3), w.Gym);
        Assert.Equal(new WidgetTallyDto(2, 2), w.Field);
        Assert.Empty(w.Left);
    }

    [Fact]
    public void NoProgramSlotsMeansZeroOfZeroAndLogsStillShow()
    {
        var w = WeekSoFarBuilder.Build(Monday, Sunday, [], [Gym(0, 4, "Gym 1")], []);

        Assert.Equal(new WidgetTallyDto(0, 0), w.Gym);
        Assert.Empty(w.Left);
        Assert.Equal(["G1"], Chips(w, 0));
    }
}
