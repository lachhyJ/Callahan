using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// How an exercise's progress is measured. Which one applies is a property of
// the exercise's own logged history, not a per-set choice - see BasisFor.
public enum LiftBasis
{
    // Estimated 1RM. The default, and right for anything in a normal working
    // rep range: it's what tracks double progression, where reps climb inside
    // a fixed prescribed range before the weight moves. Top weight alone is
    // blind to that - 240x10 -> 240x12 is real progress and no change in
    // weight at all.
    E1Rm,

    // Best set volume (weight x reps). For high-rep accessory work, where
    // Epley's extrapolation is too aggressive to trust: a 20-rep set gets
    // multiplied by 1.67, and a range as wide as 15-20 moves the estimate
    // ~17% on formula alone, swamping the actual training signal.
    SetVolume,

    // Assisted / bodyweight, where WeightKg is negative (assistance) or zero
    // (pure bodyweight). Epley cannot be used here at all: on a negative load
    // more reps makes the estimate MORE negative, so getting stronger reads
    // as getting weaker, and at exactly bodyweight it's identically zero no
    // matter what's done. Ranked by load first (the scale is already ordered
    // - less assistance is better) with reps as the tiebreak at a held load.
    Assisted,
}

public record LiftSetInput(decimal WeightKg, int Reps);

public static class LiftProgress
{
    // Above this, Epley is extrapolating further than the set can support.
    // Deliberately inclusive of 12: those slots are pinned at 12 rather than
    // spanning a range, so the estimate's bias is constant month to month and
    // cancels out of any comparison.
    public const int MaxRepsForE1Rm = 12;

    public static LiftBasis BasisFor(IReadOnlyList<LiftSetInput> history)
    {
        if (history.Count == 0) return LiftBasis.E1Rm;

        // A single non-positive load anywhere means this exercise's number
        // isn't external load - it's assistance or bodyweight. Checked across
        // the whole history so an exercise doesn't change basis the month it
        // finally reaches bodyweight.
        if (history.Any(s => s.WeightKg <= 0)) return LiftBasis.Assisted;

        return MedianReps(history) > MaxRepsForE1Rm ? LiftBasis.SetVolume : LiftBasis.E1Rm;
    }

    // Comparable within one exercise and one basis. Never compare scores
    // across exercises - only their change over time.
    public static decimal Score(LiftSetInput s, LiftBasis basis) => basis switch
    {
        LiftBasis.E1Rm => LiftMath.Epley1Rm(s.Reps, s.WeightKg),
        LiftBasis.SetVolume => s.WeightKg * s.Reps,
        // Load dominates; reps only separate sets at the same load. The 1000x
        // scaling means no plausible rep count can outrank a load change.
        LiftBasis.Assisted => s.WeightKg * 1000m + s.Reps,
        _ => 0m,
    };

    public static LiftSetInput? Best(IReadOnlyList<LiftSetInput> sets, LiftBasis basis) =>
        sets.Count == 0 ? null : sets.OrderByDescending(s => Score(s, basis)).First();

    // Percent change is only meaningful where the score is a magnitude. The
    // assisted score is a composite ordering, and its sign flips through
    // bodyweight, so a percentage of it would be nonsense - callers show the
    // before/after sets instead.
    public static decimal? DeltaPercent(LiftSetInput from, LiftSetInput to, LiftBasis basis)
    {
        if (basis == LiftBasis.Assisted) return null;
        var fromScore = Score(from, basis);
        if (fromScore <= 0) return null;
        return (Score(to, basis) - fromScore) / fromScore * 100m;
    }

    // "Nothing changed" for an assisted lift means neither the assistance nor
    // the reps moved - a percentage band can't express that.
    public static bool IsFlat(IReadOnlyList<LiftSetInput> window, LiftBasis basis, decimal thresholdPercent)
    {
        if (window.Count == 0) return false;

        if (basis == LiftBasis.Assisted)
        {
            return window.All(s => s.WeightKg == window[0].WeightKg)
                && window.All(s => s.Reps == window[0].Reps);
        }

        var scores = window.Select(s => Score(s, basis)).ToList();
        var first = scores[0];
        if (first <= 0) return false;
        return (scores.Max() - scores.Min()) / first * 100m < thresholdPercent;
    }

    public static LiftSetDto ToDto(LiftSetInput s, LiftBasis basis) =>
        new(s.WeightKg, s.Reps, basis == LiftBasis.E1Rm ? Math.Round(LiftMath.Epley1Rm(s.Reps, s.WeightKg), 1) : null);

    private static int MedianReps(IReadOnlyList<LiftSetInput> sets)
    {
        var reps = sets.Select(s => s.Reps).OrderBy(r => r).ToList();
        return reps[reps.Count / 2];
    }
}
