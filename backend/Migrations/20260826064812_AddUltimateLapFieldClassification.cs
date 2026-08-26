using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUltimateLapFieldClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldState",
                table: "ActivityLaps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AlternationViolations",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LapClassifierMethod",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LapClassifierVersion",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MixedSeconds",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OffFieldSeconds",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OnFieldDistanceM",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnFieldSeconds",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OnFieldSpeedThresholdMps",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsPlayed",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawJson",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldState",
                table: "ActivityLaps");

            migrationBuilder.DropColumn(
                name: "AlternationViolations",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LapClassifierMethod",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LapClassifierVersion",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MixedSeconds",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OffFieldSeconds",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OnFieldDistanceM",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OnFieldSeconds",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OnFieldSpeedThresholdMps",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PointsPlayed",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RawJson",
                table: "Activities");
        }
    }
}
