using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarmupSetsToWorkoutTemplateExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarmupSets",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "WarmupSets",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 2,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 3,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "WarmupSets",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 7,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 9,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 10,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "WarmupSets",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "WarmupSets",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "WarmupSets",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarmupSets",
                table: "WorkoutTemplateExercises");
        }
    }
}
