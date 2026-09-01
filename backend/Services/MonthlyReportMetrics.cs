using Callahan.Api.DTOs;

namespace Callahan.Api.Services;

// The two parts of the monthly report whose *rules* are the interesting bit,
// pulled out of MonthlyReportBuilder's DbContext-bound methods so they can be
// unit-tested directly - the ClassifyMonth / TaperPhaseCalculator pattern.

// One run, flattened to just what the running summary needs.
public record RunActivityInput(
    string TypeName,
    decimal? DistanceKm,
    int DurationSeconds,
    decimal? HighSpeedDistanceM,
    int ActiveLapCount);

// Which numbers actually mean something for each kind of run.
//
// Only continuous running earns a distance/duration total. For the two
// rep-based session types both are misleading: GPS under-measures shuttle
// turns (the same reason ConeDistanceM is entered by hand, see Activity.cs)
// and elapsed duration counts standing rest between reps, so a hard session
// and an easy one can land on the same number. Those types report the work
// instead - rep count from Garmin's own ACTIVE lap labelling, plus the
// distance covered inside those laps where it's available.
//
// An unrecognised or unset type falls back to distance/duration: it's the
// safe default for something that might be a plain continuous run.
public static class RunningMetrics
{
    private record Shape(bool Distance, bool HighSpeed, bool Reps);

    private static readonly Shape Continuous = new(Distance: true, HighSpeed: false, Reps: false);

    private static readonly Dictionary<string, Shape> ByTypeName = new()
    {
        ["Easy Aerobic Run"] = Continuous,
        ["High Speed Intervals"] = new(Distance: false, HighSpeed: true, Reps: true),
        ["Speed & Acceleration"] = new(Distance: false, HighSpeed: false, Reps: true),
    };

    public static List<RunTypeSummaryDto> Summarize(IEnumerable<RunActivityInput> runs)
    {
        return runs
            .GroupBy(r => r.TypeName)
            .Select(g =>
            {
                var shape = ByTypeName.TryGetValue(g.Key, out var s) ? s : Continuous;

                decimal? highSpeedKm = null;
                if (shape.HighSpeed)
                {
                    var withHighSpeed = g.Where(r => r.HighSpeedDistanceM is not null).ToList();
                    if (withHighSpeed.Count > 0)
                    {
                        highSpeedKm = Math.Round(withHighSpeed.Sum(r => r.HighSpeedDistanceM!.Value) / 1000m, 2);
                    }
                }

                int? reps = null;
                if (shape.Reps)
                {
                    var total = g.Sum(r => r.ActiveLapCount);
                    // No lap data synced for any session of this type - say
                    // nothing rather than claiming zero reps.
                    if (total > 0) reps = total;
                }

                return new RunTypeSummaryDto(
                    g.Key,
                    g.Count(),
                    shape.Distance ? g.Where(r => r.DistanceKm is not null).Sum(r => r.DistanceKm!.Value) : null,
                    shape.Distance ? g.Sum(r => r.DurationSeconds) : null,
                    highSpeedKm,
                    reps);
            })
            .OrderByDescending(r => r.Count)
            .ToList();
    }
}

// Prescribed vs logged set counts for one month, per side.
public record PushPullInput(int PlannedPush, int PlannedPull, int ActualPush, int ActualPull);

// Push/pull balance, as execution drift rather than as a raw ratio.
//
// A raw push:pull ratio over a fixed template program measures the program's
// designed shape - it would flag identically every month and there'd be
// nothing to act on. What's actually actionable is whether one side is being
// executed less completely than the other: prescribed sets come from each
// logged session's template (WorkoutTemplateExercise.TargetSets), logged sets
// from what was actually ticked. Comparing completion RATES rather than raw
// counts also means a light month doesn't false-positive - training less
// drags both sides down together.
public static class PushPullBalance
{
    // Gap between the two completion rates, in percentage points, below which
    // this isn't worth a line.
    private const decimal DriftThresholdPoints = 15m;

    public static string? Flag(PushPullInput i)
    {
        // Nothing prescribed on one side (no template-backed sessions this
        // month, or a program with no pulling) - no comparison to make.
        if (i.PlannedPush <= 0 || i.PlannedPull <= 0) return null;

        var pushRate = i.ActualPush / (decimal)i.PlannedPush * 100m;
        var pullRate = i.ActualPull / (decimal)i.PlannedPull * 100m;
        if (Math.Abs(pushRate - pullRate) < DriftThresholdPoints) return null;

        var pullIsLower = pullRate < pushRate;
        var lowLabel = pullIsLower ? "Pull" : "Push";
        var highLabel = pullIsLower ? "push" : "pull";
        var lowRate = pullIsLower ? pullRate : pushRate;
        var highRate = pullIsLower ? pushRate : pullRate;
        var lowActual = pullIsLower ? i.ActualPull : i.ActualPush;
        var lowPlanned = pullIsLower ? i.PlannedPull : i.PlannedPush;

        return $"{lowLabel} sets came in at {Math.Round(lowRate)}% of plan against {highLabel} at "
             + $"{Math.Round(highRate)}% — {lowActual} logged of {lowPlanned} prescribed.";
    }
}
