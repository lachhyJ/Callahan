using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeRunSessionTypeToActivitySessionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A production-data migration: RunSessionTypes -> ActivitySessionTypes
            // is a rename + add-column, not a drop/recreate, so the 3 existing
            // rows (and every Activity.RunSessionTypeId FK pointing at them) keep
            // their identity across the migration rather than being reseeded.
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_RunSessionTypes_RunSessionTypeId",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "RunSessionTypeId",
                table: "Activities",
                newName: "ActivitySessionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_RunSessionTypeId",
                table: "Activities",
                newName: "IX_Activities_ActivitySessionTypeId");

            migrationBuilder.AddColumn<int>(
                name: "ActivityType",
                table: "RunSessionTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0); // ActivityType.Running - the 3 existing rows were all running-only

            migrationBuilder.RenameTable(
                name: "RunSessionTypes",
                newName: "ActivitySessionTypes");

            migrationBuilder.InsertData(
                table: "ActivitySessionTypes",
                columns: new[] { "Id", "ActivityType", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 4, 1, "Solo", 1 },
                    { 5, 1, "Throws", 2 },
                    { 6, 1, "Pod", 3 },
                    { 7, 1, "Club Training", 4 },
                    { 8, 1, "Game", 5 }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_ActivitySessionTypes_ActivitySessionTypeId",
                table: "Activities",
                column: "ActivitySessionTypeId",
                principalTable: "ActivitySessionTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_ActivitySessionTypes_ActivitySessionTypeId",
                table: "Activities");

            migrationBuilder.DeleteData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValues: new object[] { 4, 5, 6, 7, 8 });

            migrationBuilder.RenameTable(
                name: "ActivitySessionTypes",
                newName: "RunSessionTypes");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "RunSessionTypes");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_ActivitySessionTypeId",
                table: "Activities",
                newName: "IX_Activities_RunSessionTypeId");

            migrationBuilder.RenameColumn(
                name: "ActivitySessionTypeId",
                table: "Activities",
                newName: "RunSessionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_RunSessionTypes_RunSessionTypeId",
                table: "Activities",
                column: "RunSessionTypeId",
                principalTable: "RunSessionTypes",
                principalColumn: "Id");
        }
    }
}
