using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupersetWithNext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupersetWithNext",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 2,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 3,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 7,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 9,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 10,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "SupersetWithNext",
                value: false);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "SupersetWithNext",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupersetWithNext",
                table: "WorkoutTemplateExercises");
        }
    }
}
