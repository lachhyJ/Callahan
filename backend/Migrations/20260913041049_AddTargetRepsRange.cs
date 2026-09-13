using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <summary>
    /// Structured rep-range bounds for progression readiness, parsed by hand
    /// from each row's existing TargetReps string — same house style as
    /// AddProgramV3Templates: hand-written rather than scaffolded, since only
    /// ~38 rows exist and every current value parses unambiguously. Null on
    /// both bounds means "never flag this slot": AMRAP has no ceiling by
    /// definition, and "20 secs/side" is stale free text left over from
    /// before TargetDurationSeconds existed.
    /// </summary>
    public partial class AddTargetRepsRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetRepsMax",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRepsMin",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: true);

            // --- Day A/B/C (retired v1 templates) ----------------------------------
            SetRange(migrationBuilder, 1, 5, 6);    // "5-6"
            SetRange(migrationBuilder, 2, 8, 8);    // "8/leg"
            SetRange(migrationBuilder, 3, 10, 12);  // "10-12"
            SetRange(migrationBuilder, 4, 8, 10);   // "8-10/leg"
            SetRange(migrationBuilder, 5, 8, 8);    // "8"
            SetRange(migrationBuilder, 6, 6, 8);    // "6-8"
            SetRange(migrationBuilder, 7, 10, 10);  // "10"
            SetRange(migrationBuilder, 8, 12, 12);  // "12"
            SetRange(migrationBuilder, 9, 15, 15);  // "15"
            SetRange(migrationBuilder, 10, 15, 20); // "15-20/leg"
            SetRange(migrationBuilder, 11, 6, 8);   // "6-8"
            SetRange(migrationBuilder, 12, 8, 8);   // "8/leg"
            SetRange(migrationBuilder, 13, 6, 6);   // "6"
            SetRange(migrationBuilder, 14, null, null); // "AMRAP"
            SetRange(migrationBuilder, 15, 12, 12); // "12/side"
            SetRange(migrationBuilder, 16, 5, 5);   // "5"

            // --- Gym 1/2/3 (active v3 templates) ------------------------------------
            SetRange(migrationBuilder, 17, 12, 12); // "12"
            SetRange(migrationBuilder, 18, 4, 4);   // "4"
            SetRange(migrationBuilder, 19, 8, 8);   // "8/leg"
            SetRange(migrationBuilder, 20, 8, 8);   // "8"
            SetRange(migrationBuilder, 21, 3, 3);   // "3"
            SetRange(migrationBuilder, 22, null, null); // "20 secs/side"
            SetRange(migrationBuilder, 23, 12, 12); // "12"
            SetRange(migrationBuilder, 24, 20, 20); // "20"
            SetRange(migrationBuilder, 25, 6, 6);   // "6/side"
            SetRange(migrationBuilder, 26, 8, 8);   // "8/leg"
            SetRange(migrationBuilder, 27, 8, 8);   // "8"
            SetRange(migrationBuilder, 28, 10, 10); // "10"
            SetRange(migrationBuilder, 29, 15, 15); // "15"
            SetRange(migrationBuilder, 30, 15, 15); // "15"
            SetRange(migrationBuilder, 31, 12, 12); // "12/side"
            SetRange(migrationBuilder, 32, 6, 6);   // "6"
            SetRange(migrationBuilder, 33, 8, 8);   // "8/side"
            SetRange(migrationBuilder, 34, 5, 5);   // "5"
            SetRange(migrationBuilder, 35, 5, 5);   // "5"
            SetRange(migrationBuilder, 36, 3, 4);   // "3-4"
            SetRange(migrationBuilder, 37, 8, 8);   // "8"
            SetRange(migrationBuilder, 38, 5, 5);   // "5/direction/side"
        }

        private static void SetRange(MigrationBuilder migrationBuilder, int id, int? min, int? max)
        {
            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: id,
                columns: new[] { "TargetRepsMax", "TargetRepsMin" },
                values: new object[] { max, min });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetRepsMax",
                table: "WorkoutTemplateExercises");

            migrationBuilder.DropColumn(
                name: "TargetRepsMin",
                table: "WorkoutTemplateExercises");
        }
    }
}
