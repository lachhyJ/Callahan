using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <summary>
    /// Time-based exercises: a hold measured in seconds rather than counted in
    /// reps (planks, tibialis holds), plus the per-side prompt the timer needs.
    ///
    /// Four additive columns, all nullable or defaulted, so this is a no-op over
    /// every row of existing history. EF also scaffolded ~46 UpdateData calls
    /// re-asserting IsTimeBased=false / IsPerSide=false / TargetDurationSeconds=null
    /// on the HasData seed rows (1-30, template slots 1-16); those are redundant
    /// with the column defaults and would only put an app-tuned column under seed
    /// management (same reasoning as AddProgramV3Templates - the seed block stays
    /// a record of initial state, forward changes live here). They're dropped.
    /// What remains is the one real data change: mark Copenhagen Plank (Exercise
    /// 93, added by AddProgramV3Templates) as a per-side hold and give its Gym 1
    /// slot (template exercise 22) a 20s prescription.
    /// </summary>
    public partial class AddTimeBasedExercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetDurationSeconds",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "ExerciseSets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPerSide",
                table: "Exercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTimeBased",
                table: "Exercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Copenhagen Plank - the one unambiguous time-held movement in the
            // current program ("20 secs/side" in the Gym 1 slot). Guarded so it
            // is a no-op on any database that predates AddProgramV3Templates and
            // so has no row 93 / slot 22.
            migrationBuilder.Sql(
                "UPDATE Exercises SET IsTimeBased = 1, IsPerSide = 1 WHERE Id = 93 AND Name = 'Copenhagen Plank';");
            migrationBuilder.Sql(
                "UPDATE WorkoutTemplateExercises SET TargetDurationSeconds = 20 WHERE Id = 22 AND ExerciseId = 93;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetDurationSeconds",
                table: "WorkoutTemplateExercises");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "ExerciseSets");

            migrationBuilder.DropColumn(
                name: "IsPerSide",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "IsTimeBased",
                table: "Exercises");
        }
    }
}
