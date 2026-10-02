using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingStatusToWellness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcuteLoad",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AcwrRatio",
                table: "DailyWellness",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChronicLoad",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingStatusCode",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingStatusPhrase",
                table: "DailyWellness",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Vo2Max",
                table: "DailyWellness",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcuteLoad",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "AcwrRatio",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "ChronicLoad",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "TrainingStatusCode",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "TrainingStatusPhrase",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "Vo2Max",
                table: "DailyWellness");
        }
    }
}
