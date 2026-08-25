using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConeDistanceM",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HighSpeedDistanceM",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityLaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    LapIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    IntensityType = table.Column<string>(type: "TEXT", nullable: true),
                    DistanceM = table.Column<decimal>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<decimal>(type: "TEXT", nullable: true),
                    MovingDurationSeconds = table.Column<decimal>(type: "TEXT", nullable: true),
                    AvgSpeedMps = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaxSpeedMps = table.Column<decimal>(type: "TEXT", nullable: true),
                    AvgHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxHeartRate = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLaps_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLaps_ActivityId_LapIndex",
                table: "ActivityLaps",
                columns: new[] { "ActivityId", "LapIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLaps");

            migrationBuilder.DropColumn(
                name: "ConeDistanceM",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "HighSpeedDistanceM",
                table: "Activities");
        }
    }
}
