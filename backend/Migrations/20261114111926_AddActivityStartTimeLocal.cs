using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityStartTimeLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartTimeLocal",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            // Backfill from the stored Garmin summary. Garmin's
            // "yyyy-MM-dd HH:mm:ss" is the text form EF reads a SQLite
            // DateTime from, so the string copies straight across. json_valid
            // first: json_extract throws on a malformed blob.
            migrationBuilder.Sql("""
                UPDATE Activities
                SET StartTimeLocal = json_extract(RawJson, '$.startTimeLocal')
                WHERE RawJson IS NOT NULL AND json_valid(RawJson)
                  AND length(json_extract(RawJson, '$.startTimeLocal')) = 19;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartTimeLocal",
                table: "Activities");
        }
    }
}
