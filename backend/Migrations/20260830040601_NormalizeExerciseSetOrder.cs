using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeExerciseSetOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only. ExerciseSet.SetOrder had drifted between conventions:
            // sessions logged before the 2026-08-22 warmup change are 0-based,
            // ones after are 1-based, and an early import left two calf-raise
            // groups with colliding values. Both history views render
            // `Set {setOrder + 1}`, so 0-based is correct. Re-rank every
            // (WorkoutSessionId, ExerciseId) group to a contiguous 0-based
            // sequence, ordered by the existing SetOrder then Id. Idempotent:
            // an already-clean group is unchanged.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY WorkoutSessionId, ExerciseId
               ORDER BY SetOrder, Id
           ) - 1 AS NewOrder
    FROM ExerciseSets
)
UPDATE ExerciseSets
SET SetOrder = (SELECT NewOrder FROM ranked WHERE ranked.Id = ExerciseSets.Id)
WHERE SetOrder <> (SELECT NewOrder FROM ranked WHERE ranked.Id = ExerciseSets.Id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible - the pre-migration state was inconsistent by
            // design (mixed 0-/1-based). No-op.
        }
    }
}
