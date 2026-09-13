using Callahan.Api.Models;
using Callahan.Api.Services;
using static Callahan.Api.Services.ProgressionReadinessChecker;

namespace Callahan.Api.Tests;

// Double progression, mechanically: ready only once every prescribed set
// from the last qualifying session hit the rep ceiling. Pure function of the
// slot's targets and that session's sets, so no database is needed.
public class ProgressionReadinessCheckerTests
{
    private static readonly DateOnly Today = new(2026, 9, 13);

    private static SetInput Set(int order, int reps, SetType type = SetType.Normal) => new(order, reps, type);

    [Fact]
    public void Ready_when_every_prescribed_set_hits_the_ceiling()
    {
        var slot = new SlotInput(TargetSets: 3, TargetRepsMax: 8);
        var sets = new[] { Set(1, 8), Set(2, 8), Set(3, 9) };

        var result = Evaluate(slot, Today, sets);

        Assert.True(result.Ready);
    }

    [Fact]
    public void Not_ready_when_one_set_falls_short_of_the_ceiling()
    {
        var slot = new SlotInput(TargetSets: 3, TargetRepsMax: 8);
        var sets = new[] { Set(1, 8), Set(2, 7), Set(3, 8) };

        var result = Evaluate(slot, Today, sets);

        Assert.False(result.Ready);
    }

    [Fact]
    public void Not_ready_when_fewer_qualifying_sets_than_prescribed()
    {
        var slot = new SlotInput(TargetSets: 3, TargetRepsMax: 8);
        var sets = new[] { Set(1, 8), Set(2, 8) };

        var result = Evaluate(slot, Today, sets);

        Assert.False(result.Ready);
    }

    [Fact]
    public void Null_ceiling_never_flags_regardless_of_reps_logged()
    {
        var slot = new SlotInput(TargetSets: 3, TargetRepsMax: null);
        var sets = new[] { Set(1, 50), Set(2, 50), Set(3, 50) };

        var result = Evaluate(slot, Today, sets);

        Assert.False(result.Ready);
    }

    [Fact]
    public void Warmup_sets_are_excluded_and_do_not_count_toward_target_sets()
    {
        var slot = new SlotInput(TargetSets: 2, TargetRepsMax: 10);
        var sets = new[] { Set(1, 20, SetType.Warmup), Set(2, 10), Set(3, 10) };

        var result = Evaluate(slot, Today, sets);

        Assert.True(result.Ready);
    }

    [Theory]
    [InlineData(SetType.Failure)]
    [InlineData(SetType.Drop)]
    public void Failure_and_drop_sets_count_the_same_as_normal(SetType type)
    {
        var slot = new SlotInput(TargetSets: 2, TargetRepsMax: 10);
        var sets = new[] { Set(1, 10, type), Set(2, 10, type) };

        var result = Evaluate(slot, Today, sets);

        Assert.True(result.Ready);
    }

    [Fact]
    public void No_prior_session_is_not_ready()
    {
        var slot = new SlotInput(TargetSets: 3, TargetRepsMax: 8);

        var result = Evaluate(slot, null, []);

        Assert.False(result.Ready);
    }

    [Fact]
    public void Only_the_first_target_sets_worth_of_sets_are_checked()
    {
        // A logged extra set beyond the prescribed count doesn't drag the
        // flag down if it happens to fall short.
        var slot = new SlotInput(TargetSets: 2, TargetRepsMax: 8);
        var sets = new[] { Set(1, 8), Set(2, 8), Set(3, 1) };

        var result = Evaluate(slot, Today, sets);

        Assert.True(result.Ready);
    }
}
