using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class MergeTaperEventIntoTournament : Migration
    {
        /// <inheritdoc />
        // Folds TaperEvent into Tournament: one row is now both the thing you
        // taper toward and the grouping label for the games played at it.
        //
        // The TaperEventId -> TournamentId rename below carries old TaperEvent
        // ids into a column that now references Tournaments. That is only safe
        // because TaperEvents, TaperCheckIns and TaperReminderLogs were all
        // verified empty in production (2026-09-04) before this was written -
        // there is nothing to remap. If this migration is ever replayed against
        // a database that DOES hold taper rows, it will produce dangling
        // references and needs a data step first.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaperCheckIns_TaperEvents_TaperEventId",
                table: "TaperCheckIns");

            migrationBuilder.DropForeignKey(
                name: "FK_TaperReminderLogs_TaperEvents_TaperEventId",
                table: "TaperReminderLogs");

            migrationBuilder.DropTable(
                name: "TaperEvents");

            migrationBuilder.RenameColumn(
                name: "TaperEventId",
                table: "TaperReminderLogs",
                newName: "TournamentId");

            migrationBuilder.RenameIndex(
                name: "IX_TaperReminderLogs_TaperEventId_Date",
                table: "TaperReminderLogs",
                newName: "IX_TaperReminderLogs_TournamentId_Date");

            migrationBuilder.RenameColumn(
                name: "TaperEventId",
                table: "TaperCheckIns",
                newName: "TournamentId");

            migrationBuilder.RenameIndex(
                name: "IX_TaperCheckIns_TaperEventId_Date",
                table: "TaperCheckIns",
                newName: "IX_TaperCheckIns_TournamentId_Date");

            migrationBuilder.AddColumn<double>(
                name: "PlannedReductionPercent",
                table: "Tournaments",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaperDays",
                table: "Tournaments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaperCheckIns_Tournaments_TournamentId",
                table: "TaperCheckIns",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaperReminderLogs_Tournaments_TournamentId",
                table: "TaperReminderLogs",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaperCheckIns_Tournaments_TournamentId",
                table: "TaperCheckIns");

            migrationBuilder.DropForeignKey(
                name: "FK_TaperReminderLogs_Tournaments_TournamentId",
                table: "TaperReminderLogs");

            migrationBuilder.DropColumn(
                name: "PlannedReductionPercent",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TaperDays",
                table: "Tournaments");

            migrationBuilder.RenameColumn(
                name: "TournamentId",
                table: "TaperReminderLogs",
                newName: "TaperEventId");

            migrationBuilder.RenameIndex(
                name: "IX_TaperReminderLogs_TournamentId_Date",
                table: "TaperReminderLogs",
                newName: "IX_TaperReminderLogs_TaperEventId_Date");

            migrationBuilder.RenameColumn(
                name: "TournamentId",
                table: "TaperCheckIns",
                newName: "TaperEventId");

            migrationBuilder.RenameIndex(
                name: "IX_TaperCheckIns_TournamentId_Date",
                table: "TaperCheckIns",
                newName: "IX_TaperCheckIns_TaperEventId_Date");

            migrationBuilder.CreateTable(
                name: "TaperEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    PlannedReductionPercent = table.Column<double>(type: "REAL", nullable: true),
                    TaperDays = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaperEvents", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_TaperCheckIns_TaperEvents_TaperEventId",
                table: "TaperCheckIns",
                column: "TaperEventId",
                principalTable: "TaperEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaperReminderLogs_TaperEvents_TaperEventId",
                table: "TaperReminderLogs",
                column: "TaperEventId",
                principalTable: "TaperEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
