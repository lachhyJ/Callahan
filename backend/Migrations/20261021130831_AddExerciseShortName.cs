using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseShortName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Exercises",
                type: "TEXT",
                nullable: true);

            // ShortName is runtime-owned (edited on the exercise page), so it is
            // not part of the HasData seed. These are a one-off backfill for the
            // names that run long on the watch; keyed by Name, a no-op where a
            // name is absent, and never overwriting a value already set.
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Leg Hamstring Curl' WHERE Name = 'Single Leg Hamstring Curl (Alternating)' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Leg Hamstring Curl' WHERE Name = 'Single Leg Hamstring Curl' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Leg Balance Reach' WHERE Name = 'Single-Leg Balance + Reach (Y-Pattern)' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Leg Calf Raise' WHERE Name = 'Single Leg Standing Calf Raise' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Arm Cable Cross' WHERE Name = 'Single Arm Cable Crossover' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Arm Cable Row' WHERE Name = 'Single Arm Cable Row' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = '1-Leg RDL' WHERE Name = 'Single Leg RDL (DB)' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = 'Bulgarian Squat' WHERE Name = 'Bulgarian Split Squat (Barbell)' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = 'Side Plank Reach' WHERE Name = 'Side Plank with Reach' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = 'Rope Pushdown' WHERE Name = 'Triceps Rope Pushdown' AND ShortName IS NULL;");
            migrationBuilder.Sql("UPDATE Exercises SET ShortName = 'Cable Fly' WHERE Name = 'Cable Fly Crossovers' AND ShortName IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Exercises");
        }
    }
}
