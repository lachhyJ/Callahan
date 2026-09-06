using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Cadence = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutineCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoutineId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutineCompletions_Routines_RoutineId",
                        column: x => x.RoutineId,
                        principalTable: "Routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoutineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoutineId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Prescription = table.Column<string>(type: "TEXT", nullable: false),
                    Cue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutineItems_Routines_RoutineId",
                        column: x => x.RoutineId,
                        principalTable: "Routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Routines",
                columns: new[] { "Id", "Cadence", "Name", "Notes", "Purpose", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Every day, including rest days. Five minutes.", "Daily Ankle Circuit", "This is not an accessory block. Three years of recurrence with no structural diagnosis; the working assumption is functional instability, which responds to proprioceptive and balance work. Highest value, lowest cost thing in the program.\n\nProgression ladder - move up when the current level feels easy, roughly every 2-3 weeks:\n- Eyes open, firm ground\n- Eyes closed, firm ground\n- Unstable surface (pillow, folded mat)\n- Unstable surface + ball toss against a wall\n- Single-leg hop and stick, multi-directional\n\nKeep the braces for games and pod sessions. Braces and neuromuscular work are complementary, not alternatives.", "Proprioceptive work for the ankle - the foundation of the program", 1 },
                    { 2, "2x weekly, 15 mins - before Gym 1 and Gym 3", "Jump Block", "Not a gym session. These need overhead space, a run-up, or somewhere you can land hard, which a commercial gym floor isn't. Do them at home, in a driveway, a park, or anywhere with a basketball ring, then head to the gym. Pogos and skater bounds stay in the gym - they're low, quiet and nobody looks twice.\n\nIntroduce it over 3 weeks, not in one go:\n- Week 1: CMJ 2x3 only. No depth drops.\n- Week 2: CMJ 3x3 + depth drops 2x3 from a low step.\n- Week 3: full prescription.\n- Progress depth-drop height only after 4 weeks of clean, silent landings.\n\nOne-leg approach jumps stay in Field 1, where they already are. They need a real run-up and a high target, and Field 1 is the only place you're genuinely fresh. Don't add them here as well - one-leg landing volume is the thing you least want to spike with a three-year ankle history.\n\nThe adherence risk: a 15-minute thing that happens before you leave the house is the easiest thing in this program to skip. Tie it to leaving, not to a time.", "Vertical work - the number one priority, kept out of the gym", 2 }
                });

            migrationBuilder.InsertData(
                table: "RoutineItems",
                columns: new[] { "Id", "Cue", "ItemOrder", "Name", "Prescription", "RoutineId" },
                values: new object[,]
                {
                    { 1, null, 1, "Single-leg balance, eyes open", "45 secs/side", 1 },
                    { 2, null, 2, "Single-leg balance, eyes closed", "30 secs/side", 1 },
                    { 3, null, 3, "Single-leg balance + head turns", "20 secs/side", 1 },
                    { 4, null, 4, "Banded ankle eversion", "15/side", 1 },
                    { 5, null, 5, "Tibialis raises", "20", 1 },
                    { 6, null, 6, "Single-leg calf raise, slow", "12/side", 1 },
                    { 7, "Balance + calf raise. Warm the ankle before you land on it.", 1, "Ankle circuit (abbreviated)", "", 2 },
                    { 8, "Warm-up, low", 2, "Pogo hops", "2x10", 2 },
                    { 9, "Measure weekly. Mark a fixed point - a wall, a doorframe, a ring. Full recovery.", 3, "Countermovement jump to target", "4x3", 2 },
                    { 10, "A low step to start (~30cm). Absorb, freeze, hold 2 secs.", 4, "Depth drop to stick landing", "3x5", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutineCompletions_RoutineId_Date",
                table: "RoutineCompletions",
                columns: new[] { "RoutineId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutineItems_RoutineId",
                table: "RoutineItems",
                column: "RoutineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutineCompletions");

            migrationBuilder.DropTable(
                name: "RoutineItems");

            migrationBuilder.DropTable(
                name: "Routines");
        }
    }
}
