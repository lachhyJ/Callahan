using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkoutTemplatesToDayLettersWithSubtitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "WorkoutTemplates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Subtitle" },
                values: new object[] { "Day A", "Lower & Power" });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Name", "Subtitle" },
                values: new object[] { "Day B", "Upper" });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Subtitle" },
                values: new object[] { "Day C", "Full Body Athletic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "WorkoutTemplates");

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Workout 1 — Lower & Power");

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Workout 2 — Upper");

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Workout 3 — Full Body Athletic");
        }
    }
}
