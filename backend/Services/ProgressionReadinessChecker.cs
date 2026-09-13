using Callahan.Api.Models;

namespace Callahan.Api.Services;

// Double progression, mechanically: a slot is ready to add weight once every
// prescribed set from the most recent qualifying session hit the top of the
// rep range. Pure: takes the slot's own targets and that session's already-
// loaded sets, so it can run identically from Start() and the exercise detail
// endpoint without a second query.
public static class ProgressionReadinessChecker
{
    public record SlotInput(int TargetSets, int? TargetRepsMax);

    public record SetInput(int SetOrder, int Reps, SetType SetType);

    public record Result(
        bool Ready,
        int TargetSets,
        int? TargetRepsMax,
        DateOnly? LastSessionDate,
        IReadOnlyList<SetInput> LastSessionSets);

    public static Result Evaluate(SlotInput slot, DateOnly? lastSessionDate, IReadOnlyCollection<SetInput> lastSessionSets)
    {
        if (slot.TargetRepsMax is null)
        {
            return new Result(false, slot.TargetSets, null, lastSessionDate, []);
        }

        var qualifyingSets = lastSessionSets
            .Where(s => s.SetType != SetType.Warmup)
            .OrderBy(s => s.SetOrder)
            .ToList();

        var ready = qualifyingSets.Count >= slot.TargetSets
            && qualifyingSets.Take(slot.TargetSets).All(s => s.Reps >= slot.TargetRepsMax);

        return new Result(ready, slot.TargetSets, slot.TargetRepsMax, lastSessionDate, qualifyingSets);
    }
}
