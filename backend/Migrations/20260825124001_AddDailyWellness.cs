using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWellness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyWellness",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SleepSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    DeepSleepSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    LightSleepSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    RemSleepSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AwakeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    SleepScore = table.Column<int>(type: "INTEGER", nullable: true),
                    SleepScoreQualifier = table.Column<string>(type: "TEXT", nullable: true),
                    HrvLastNightAvg = table.Column<int>(type: "INTEGER", nullable: true),
                    HrvWeeklyAvg = table.Column<int>(type: "INTEGER", nullable: true),
                    HrvStatus = table.Column<string>(type: "TEXT", nullable: true),
                    TrainingReadinessScore = table.Column<int>(type: "INTEGER", nullable: true),
                    TrainingReadinessLevel = table.Column<string>(type: "TEXT", nullable: true),
                    TrainingReadinessFeedback = table.Column<string>(type: "TEXT", nullable: true),
                    RestingHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    BodyBatteryHigh = table.Column<int>(type: "INTEGER", nullable: true),
                    BodyBatteryLow = table.Column<int>(type: "INTEGER", nullable: true),
                    AvgStressLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWellness", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWellness_Date",
                table: "DailyWellness",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWellness");
        }
    }
}
