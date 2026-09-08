using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTypeFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Family",
                table: "ActivitySessionTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Family",
                value: 0);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "Family",
                value: 0);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Family",
                value: 0);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Family",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "Family",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Family",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Family",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "Family",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Family",
                value: 1);

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Family",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Family",
                table: "ActivitySessionTypes");
        }
    }
}
