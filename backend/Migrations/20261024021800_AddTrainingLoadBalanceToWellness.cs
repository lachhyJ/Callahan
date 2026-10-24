using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingLoadBalanceToWellness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BalanceAerobicHigh",
                table: "DailyWellness",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAerobicHighMax",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAerobicHighMin",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BalanceAerobicLow",
                table: "DailyWellness",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAerobicLowMax",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAerobicLowMin",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BalanceAnaerobic",
                table: "DailyWellness",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAnaerobicMax",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAnaerobicMin",
                table: "DailyWellness",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalancePhrase",
                table: "DailyWellness",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceAerobicHigh",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAerobicHighMax",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAerobicHighMin",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAerobicLow",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAerobicLowMax",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAerobicLowMin",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAnaerobic",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAnaerobicMax",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalanceAnaerobicMin",
                table: "DailyWellness");

            migrationBuilder.DropColumn(
                name: "BalancePhrase",
                table: "DailyWellness");
        }
    }
}
