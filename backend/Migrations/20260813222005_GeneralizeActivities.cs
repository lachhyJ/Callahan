using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RunningSessions",
                newName: "Activities");

            // SQLite stores DECIMAL as TEXT already, so widening to nullable is a no-op
            // on existing data — every current row keeps its DistanceKm value as-is.
            migrationBuilder.AlterColumn<decimal>(
                name: "DistanceKm",
                table: "Activities",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0); // ActivityType.Running — every existing row is a run

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0); // ActivitySource.Manual — every existing row was hand-entered

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvgHeartRate",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminActivityId",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_GarminActivityId",
                table: "Activities",
                column: "GarminActivityId",
                unique: true,
                filter: "[GarminActivityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_GarminActivityId",
                table: "Activities");

            migrationBuilder.DropColumn(name: "GarminActivityId", table: "Activities");
            migrationBuilder.DropColumn(name: "AvgHeartRate", table: "Activities");
            migrationBuilder.DropColumn(name: "Calories", table: "Activities");
            migrationBuilder.DropColumn(name: "Source", table: "Activities");
            migrationBuilder.DropColumn(name: "Type", table: "Activities");

            migrationBuilder.AlterColumn<decimal>(
                name: "DistanceKm",
                table: "Activities",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.RenameTable(
                name: "Activities",
                newName: "RunningSessions");
        }
    }
}
