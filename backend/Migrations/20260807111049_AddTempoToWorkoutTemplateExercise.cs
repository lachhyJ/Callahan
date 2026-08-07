using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTempoToWorkoutTemplateExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tempo",
                table: "WorkoutTemplateExercises",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 2,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 3,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 7,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 9,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 10,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "Tempo",
                value: null);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "Tempo",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tempo",
                table: "WorkoutTemplateExercises");
        }
    }
}
