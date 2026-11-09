using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProgramV4Routines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Program v4 retires the Jump Block (its jumps open Field 1 / Field 2). A plan slot
            // restricts deleting the routine it points at, so the Jump Block's planner slots go
            // first (their per-week deviation rows cascade). Down() restores the routine but
            // not these slots: they were planner placement, not history.
            migrationBuilder.Sql("DELETE FROM PlanSlots WHERE RoutineId = 2;");

            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Routines",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "ItemOrder",
                value: 5);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "ItemOrder",
                value: 6);

            migrationBuilder.InsertData(
                table: "RoutineItems",
                columns: new[] { "Id", "Cue", "HoldSeconds", "IsPerSide", "ItemOrder", "Name", "PerSideDelaySeconds", "Prescription", "RoutineId" },
                values: new object[] { 11, null, null, false, 4, "Single-leg balance + reach (Y-pattern)", null, "3 reaches/direction/side", 1 });

            migrationBuilder.UpdateData(
                table: "Routines",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Cadence", "Name" },
                values: new object[] { "Start of every session - gym, field, pod, club. At least 3 a week. Five minutes.", "Ankle Circuit" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "ItemOrder",
                value: 4);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "ItemOrder",
                value: 5);

            migrationBuilder.InsertData(
                table: "RoutineItems",
                columns: new[] { "Id", "Cue", "HoldSeconds", "IsPerSide", "ItemOrder", "Name", "PerSideDelaySeconds", "Prescription", "RoutineId" },
                values: new object[] { 6, null, null, false, 6, "Single-leg calf raise, slow", null, "12/side", 1 });

            migrationBuilder.UpdateData(
                table: "Routines",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Cadence", "Name" },
                values: new object[] { "Every day, including rest days. Five minutes.", "Daily Ankle Circuit" });

            migrationBuilder.InsertData(
                table: "Routines",
                columns: new[] { "Id", "Cadence", "Name", "Notes", "Purpose", "SortOrder" },
                values: new object[] { 2, "2x weekly, 15 mins - before Gym 1 and Gym 3", "Jump Block", "Not a gym session. These need overhead space, a run-up, or somewhere you can land hard, which a commercial gym floor isn't. Do them at home, in a driveway, a park, or anywhere with a basketball ring, then head to the gym. Pogos and skater bounds stay in the gym - they're low, quiet and nobody looks twice.\n\nIntroduce it over 3 weeks, not in one go:\n- Week 1: CMJ 2x3 only. No depth drops.\n- Week 2: CMJ 3x3 + depth drops 2x3 from a low step.\n- Week 3: full prescription.\n- Progress depth-drop height only after 4 weeks of clean, silent landings.\n\nOne-leg approach jumps stay in Field 1, where they already are. They need a real run-up and a high target, and Field 1 is the only place you're genuinely fresh. Don't add them here as well - one-leg landing volume is the thing you least want to spike with a three-year ankle history.\n\nThe adherence risk: a 15-minute thing that happens before you leave the house is the easiest thing in this program to skip. Tie it to leaving, not to a time.", "Vertical work - the number one priority, kept out of the gym", 2 });

            migrationBuilder.InsertData(
                table: "RoutineItems",
                columns: new[] { "Id", "Cue", "HoldSeconds", "IsPerSide", "ItemOrder", "Name", "PerSideDelaySeconds", "Prescription", "RoutineId" },
                values: new object[,]
                {
                    { 7, "Balance + calf raise. Warm the ankle before you land on it.", null, false, 1, "Ankle circuit (abbreviated)", null, "", 2 },
                    { 8, "Warm-up, low", null, false, 2, "Pogo hops", null, "2x10", 2 },
                    { 9, "Measure weekly. Mark a fixed point - a wall, a doorframe, a ring. Full recovery.", null, false, 3, "Countermovement jump to target", null, "4x3", 2 },
                    { 10, "A low step to start (~30cm). Absorb, freeze, hold 2 secs.", null, false, 4, "Depth drop to stick landing", null, "3x5", 2 }
                });
        }
    }
}
