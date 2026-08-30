using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseSetOrderUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExerciseSets_WorkoutSessionId",
                table: "ExerciseSets");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSets_WorkoutSessionId_ExerciseId_SetOrder",
                table: "ExerciseSets",
                columns: new[] { "WorkoutSessionId", "ExerciseId", "SetOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExerciseSets_WorkoutSessionId_ExerciseId_SetOrder",
                table: "ExerciseSets");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSets_WorkoutSessionId",
                table: "ExerciseSets",
                column: "WorkoutSessionId");
        }
    }
}
