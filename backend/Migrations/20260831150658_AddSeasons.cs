using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeasonId",
                table: "Tournaments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TargetTournamentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Tournaments_TargetTournamentId",
                        column: x => x.TargetTournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_SeasonId",
                table: "Tournaments",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_StartDate",
                table: "Seasons",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_TargetTournamentId",
                table: "Seasons",
                column: "TargetTournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Seasons_SeasonId",
                table: "Tournaments",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Seasons_SeasonId",
                table: "Tournaments");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_SeasonId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "Tournaments");
        }
    }
}
