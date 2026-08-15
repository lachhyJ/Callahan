using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRunSessionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunSessionTypeId",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RunSessionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSessionTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RunSessionTypes",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "High Speed Intervals", 1 },
                    { 2, "Speed & Acceleration", 2 },
                    { 3, "Easy Aerobic Run", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_RunSessionTypeId",
                table: "Activities",
                column: "RunSessionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_RunSessionTypes_RunSessionTypeId",
                table: "Activities",
                column: "RunSessionTypeId",
                principalTable: "RunSessionTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_RunSessionTypes_RunSessionTypeId",
                table: "Activities");

            migrationBuilder.DropTable(
                name: "RunSessionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Activities_RunSessionTypeId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RunSessionTypeId",
                table: "Activities");
        }
    }
}
