using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutineItemTimers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HoldSeconds",
                table: "RoutineItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPerSide",
                table: "RoutineItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PerSideDelaySeconds",
                table: "RoutineItems",
                type: "INTEGER",
                nullable: true);

            // Only items 1-3 have a parseable "<n> secs/side" prescription; the rest
            // (reps, or step-only) keep the column defaults (null/false/null).
            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "HoldSeconds", "IsPerSide", "PerSideDelaySeconds" },
                values: new object[] { 45, true, 8 });

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "HoldSeconds", "IsPerSide", "PerSideDelaySeconds" },
                values: new object[] { 30, true, 8 });

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "HoldSeconds", "IsPerSide", "PerSideDelaySeconds" },
                values: new object[] { 20, true, 8 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoldSeconds",
                table: "RoutineItems");

            migrationBuilder.DropColumn(
                name: "IsPerSide",
                table: "RoutineItems");

            migrationBuilder.DropColumn(
                name: "PerSideDelaySeconds",
                table: "RoutineItems");
        }
    }
}
