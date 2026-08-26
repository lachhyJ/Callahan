using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTrackAndLapStartTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartTimeGmt",
                table: "ActivityLaps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartEpochMs = table.Column<long>(type: "INTEGER", nullable: false),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MedianSpacingSec = table.Column<decimal>(type: "TEXT", nullable: true),
                    SamplesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityTracks_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTracks_ActivityId",
                table: "ActivityTracks",
                column: "ActivityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityTracks");

            migrationBuilder.DropColumn(
                name: "StartTimeGmt",
                table: "ActivityLaps");
        }
    }
}
