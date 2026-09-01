using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameBarbellSquatExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 19,
                column: "Name",
                value: "Barbell Squat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 19,
                column: "Name",
                value: "Barbell or Trap Bar Squat");
        }
    }
}
