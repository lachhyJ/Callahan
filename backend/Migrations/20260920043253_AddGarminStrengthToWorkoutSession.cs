using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGarminStrengthToWorkoutSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GarminActivityId",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GarminActivityTrainingLoad",
                table: "WorkoutSessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GarminAerobicTrainingEffect",
                table: "WorkoutSessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GarminAnaerobicTrainingEffect",
                table: "WorkoutSessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GarminAvgHeartRate",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GarminCalories",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GarminDurationSeconds",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminRawJson",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GarminTrainingEffectLabel",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingGarminStrengthActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GarminActivityId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Calories = table.Column<int>(type: "INTEGER", nullable: true),
                    AvgHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    ActivityTrainingLoad = table.Column<double>(type: "REAL", nullable: true),
                    AerobicTrainingEffect = table.Column<double>(type: "REAL", nullable: true),
                    AnaerobicTrainingEffect = table.Column<double>(type: "REAL", nullable: true),
                    TrainingEffectLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingGarminStrengthActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_GarminActivityId",
                table: "WorkoutSessions",
                column: "GarminActivityId",
                unique: true,
                filter: "[GarminActivityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PendingGarminStrengthActivities_GarminActivityId",
                table: "PendingGarminStrengthActivities",
                column: "GarminActivityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingGarminStrengthActivities");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_GarminActivityId",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminActivityId",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminActivityTrainingLoad",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminAerobicTrainingEffect",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminAnaerobicTrainingEffect",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminAvgHeartRate",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminCalories",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminDurationSeconds",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminRawJson",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "GarminTrainingEffectLabel",
                table: "WorkoutSessions");
        }
    }
}
