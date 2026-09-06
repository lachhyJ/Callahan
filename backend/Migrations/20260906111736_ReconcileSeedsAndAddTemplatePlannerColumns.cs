using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileSeedsAndAddTemplatePlannerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                table: "WorkoutTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LowerBodyLoad",
                table: "WorkoutTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriorityOrder",
                table: "WorkoutTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsAssisted",
                value: true);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Bulgarian Split Squat (Barbell)");

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsAssisted",
                value: true);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsRetired", "LowerBodyLoad", "PriorityOrder" },
                values: new object[] { false, 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsRetired", "LowerBodyLoad", "PriorityOrder" },
                values: new object[] { false, 0, 0 });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsRetired", "LowerBodyLoad", "PriorityOrder" },
                values: new object[] { false, 0, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRetired",
                table: "WorkoutTemplates");

            migrationBuilder.DropColumn(
                name: "LowerBodyLoad",
                table: "WorkoutTemplates");

            migrationBuilder.DropColumn(
                name: "PriorityOrder",
                table: "WorkoutTemplates");

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsAssisted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Bulgarian Split Squat (DB)");

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsAssisted",
                value: false);

            migrationBuilder.InsertData(
                table: "Exercises",
                columns: new[] { "Id", "Category", "IsAssisted", "Name" },
                values: new object[,]
                {
                    { 2, 0, false, "Overhead Press" },
                    { 3, 0, false, "Push-up" },
                    { 7, 2, false, "Squat" },
                    { 9, 2, false, "Lunges" },
                    { 10, 3, false, "Plank" }
                });
        }
    }
}
