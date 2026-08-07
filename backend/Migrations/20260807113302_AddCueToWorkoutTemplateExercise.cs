using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCueToWorkoutTemplateExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cue",
                table: "WorkoutTemplateExercises",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 2,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 3,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 7,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 9,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 10,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "Cue",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cue",
                table: "WorkoutTemplateExercises");
        }
    }
}
