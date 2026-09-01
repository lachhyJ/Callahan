namespace Callahan.Api.Services;

// Shared strength-math helpers. Kept deliberately tiny — just the formulae, no
// bucketing or querying — so every caller agrees on the numbers.
public static class LiftMath
{
    // Epley estimated 1RM: weight * (1 + reps / 30). Matches the definition
    // previously inlined in ExercisesController.GetStats and
    // MonthlyReportBuilder.E1Rm.
    public static decimal Epley1Rm(int reps, decimal weightKg) => weightKg * (1 + reps / 30m);
}
