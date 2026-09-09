using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGarminActivityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ActivityTrainingLoad",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AerobicTrainingEffect",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnaerobicTrainingEffect",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingEffectLabel",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityTrainingLoad",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AerobicTrainingEffect",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AnaerobicTrainingEffect",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TrainingEffectLabel",
                table: "Activities");
        }
    }
}
