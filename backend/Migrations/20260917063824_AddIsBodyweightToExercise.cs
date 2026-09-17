using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBodyweightToExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBodyweight",
                table: "Exercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 20,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 21,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 23,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 26,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 27,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 29,
                column: "IsBodyweight",
                value: false);

            migrationBuilder.UpdateData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 30,
                column: "IsBodyweight",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBodyweight",
                table: "Exercises");
        }
    }
}
