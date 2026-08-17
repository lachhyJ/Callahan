using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaperCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaperCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaperEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Energy = table.Column<int>(type: "INTEGER", nullable: false),
                    Soreness = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivation = table.Column<int>(type: "INTEGER", nullable: false),
                    Context = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaperCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaperCheckIns_TaperEvents_TaperEventId",
                        column: x => x.TaperEventId,
                        principalTable: "TaperEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaperReminderLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaperEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaperReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaperReminderLogs_TaperEvents_TaperEventId",
                        column: x => x.TaperEventId,
                        principalTable: "TaperEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaperCheckIns_TaperEventId_Date",
                table: "TaperCheckIns",
                columns: new[] { "TaperEventId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaperReminderLogs_TaperEventId_Date",
                table: "TaperReminderLogs",
                columns: new[] { "TaperEventId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaperCheckIns");

            migrationBuilder.DropTable(
                name: "TaperReminderLogs");
        }
    }
}
