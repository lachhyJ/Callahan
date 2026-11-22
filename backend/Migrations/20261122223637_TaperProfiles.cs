using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaperProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KeepInTaper",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TaperProfile",
                table: "Tournaments",
                type: "INTEGER",
                nullable: true);

            // Every taper planned so far becomes a Regionals taper (1), the shape
            // closest to the old single ladder for a short taper, with its planned
            // cut re-stamped to Regionals' 30%.
            migrationBuilder.Sql("UPDATE Tournaments SET TaperProfile = 1, PlannedReductionPercent = 0.3 WHERE TaperDays IS NOT NULL;");

            // Main lifts and jumps a Nationals taper keeps: trap bar deadlift (11),
            // barbell squat (19), pogo jumps (67). The seeded v1 rows (ids 1, 11)
            // are left to their HasData value so the model and the data agree;
            // those templates are retired anyway.
            migrationBuilder.Sql("UPDATE WorkoutTemplateExercises SET KeepInTaper = 1 WHERE ExerciseId IN (11, 19, 67) AND Id NOT IN (1, 11);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeepInTaper",
                table: "WorkoutTemplateExercises");

            migrationBuilder.DropColumn(
                name: "TaperProfile",
                table: "Tournaments");
        }
    }
}
