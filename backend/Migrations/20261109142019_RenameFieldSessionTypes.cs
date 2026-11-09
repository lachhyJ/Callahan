using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameFieldSessionTypes : Migration
    {
        // The field sessions are just "Field 1" and "Field 2" in the app. The program doc's
        // headings keep the subtitle ("Field 1 — Defence & Acceleration"), and that is what
        // the run sheet on Routines shows. Planner slots are seeded by migration, not HasData,
        // so their labels are renamed here by hand.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "Field 1");

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "Field 2");

            migrationBuilder.Sql("UPDATE PlanSlots SET Label = 'Field 1' WHERE Kind = 1 AND ActivitySessionTypeId = 9;");
            migrationBuilder.Sql("UPDATE PlanSlots SET Label = 'Field 2' WHERE Kind = 1 AND ActivitySessionTypeId = 10;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "Field 1 - Acceleration & Jump Quality");

            migrationBuilder.UpdateData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "Field 2 - Repeat Effort & COD");

            migrationBuilder.Sql("UPDATE PlanSlots SET Label = 'Field 1 - Defence & Acceleration' WHERE Kind = 1 AND ActivitySessionTypeId = 9;");
            migrationBuilder.Sql("UPDATE PlanSlots SET Label = 'Field 2 - Repeat Effort & COD' WHERE Kind = 1 AND ActivitySessionTypeId = 10;");
        }
    }
}
