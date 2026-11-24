using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnkleCircuitCues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "Cue",
                value: "Firm floor, soft knee, hips level, eyes on one fixed spot. If you touch down, put the foot back and carry on.");

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "Cue",
                value: "Same stance, eyes shut, wall or bench within reach. Small constant wobbles at the ankle are the work, so stay loose. Your time to failure on each side is one of the program's tests.");

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "Cue",
                value: "Eyes open. Turn your head slowly left and right while hips and standing knee stay still.");

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "Cue",
                value: "Seated, band round the forefoot anchored on the inside of that foot. Turn the sole outward against the band, moving only at the ankle; about 2 secs back.");

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "Cue",
                value: "Back to a wall, feet about a foot and a half out, heels down. Pull the toes up toward the shins as high as they go; lower under control.");

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 11,
                column: "Cue",
                value: "Tap the free foot as far as you can reach forward, then behind and across the standing leg, then behind and out to the side, back to the start each time. Light tap, no weight on it.");

            migrationBuilder.UpdateData(
                table: "Routines",
                keyColumn: "Id",
                keyValue: 1,
                column: "Notes",
                value: "This is not an accessory block. Three years of recurrence with no structural diagnosis; the working assumption is functional instability, which responds to proprioceptive and balance work. Highest value, lowest cost thing in the program.\n\nWhere and how: the start of every session, at least 3 a week, usually 4-5. Barefoot where practical (shoes off on a gym mat, barefoot or socks on grass), but done in shoes beats skipped. Keep a band in your bag.\n\nWhy 3 a week: the trial behind this (Hupperets 2009) used three sessions a week for eight weeks built into the warm-up, and recurrence fell by about 35%. Plausible for you, not promised. If it recurs after 3+ months of at least 3 ticked sessions a week, the functional-instability assumption is probably wrong, and imaging or a third opinion is worth pushing for.\n\nProgression ladder - move up when the current level feels easy, roughly every 2-3 weeks:\n- Eyes open, firm ground\n- Eyes closed, firm ground\n- Unstable surface (pillow, folded mat, or BOSU dome-side-up)\n- Unstable surface + ball toss against a wall\n- Single-leg hop and stick, multi-directional - firm ground matters more than surface wobble; you land on grass in cleats, not on a dome\n\nKeep the braces for games and pod sessions. Braces and neuromuscular work are complementary, not alternatives.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "RoutineItems",
                keyColumn: "Id",
                keyValue: 11,
                column: "Cue",
                value: null);

            migrationBuilder.UpdateData(
                table: "Routines",
                keyColumn: "Id",
                keyValue: 1,
                column: "Notes",
                value: "This is not an accessory block. Three years of recurrence with no structural diagnosis; the working assumption is functional instability, which responds to proprioceptive and balance work. Highest value, lowest cost thing in the program.\n\nProgression ladder - move up when the current level feels easy, roughly every 2-3 weeks:\n- Eyes open, firm ground\n- Eyes closed, firm ground\n- Unstable surface (pillow, folded mat)\n- Unstable surface + ball toss against a wall\n- Single-leg hop and stick, multi-directional\n\nKeep the braces for games and pod sessions. Braces and neuromuscular work are complementary, not alternatives.");
        }
    }
}
