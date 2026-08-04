using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinishersAndSessionTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finishers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExerciseId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetSets = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetReps = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finishers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finishers_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Exercises",
                columns: new[] { "Id", "Category", "Name" },
                values: new object[,]
                {
                    { 25, 3, "Dead Bug" },
                    { 26, 3, "Cable Woodchop" },
                    { 27, 3, "Hanging Knee Raise" },
                    { 28, 3, "Ab Wheel Rollout" },
                    { 29, 3, "Cable Crunch" },
                    { 30, 3, "Side Plank with Reach" }
                });

            migrationBuilder.InsertData(
                table: "Finishers",
                columns: new[] { "Id", "ExerciseId", "SortOrder", "TargetReps", "TargetSets" },
                values: new object[,]
                {
                    { 1, 25, 1, "8/side", 3 },
                    { 2, 26, 2, "12/side", 3 },
                    { 3, 27, 3, "12", 3 },
                    { 4, 28, 4, "8-10", 3 },
                    { 5, 29, 5, "15", 3 },
                    { 6, 30, 6, "30 secs/side", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finishers_ExerciseId",
                table: "Finishers",
                column: "ExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finishers");

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "WorkoutSessions");
        }
    }
}
