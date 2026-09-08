using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySessionTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivitySessionTags",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivitySessionTypeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySessionTags", x => new { x.ActivityId, x.ActivitySessionTypeId });
                    table.ForeignKey(
                        name: "FK_ActivitySessionTags_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivitySessionTags_ActivitySessionTypes_ActivitySessionTypeId",
                        column: x => x.ActivitySessionTypeId,
                        principalTable: "ActivitySessionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySessionTags_ActivitySessionTypeId",
                table: "ActivitySessionTags",
                column: "ActivitySessionTypeId");

            // Backfill: the existing single classification becomes the first tag
            // (and stays the primary via Activities.ActivitySessionTypeId). Keeps
            // the "primary is always also a tag row" invariant true for every
            // already-classified activity.
            migrationBuilder.Sql(@"
                INSERT INTO ActivitySessionTags (ActivityId, ActivitySessionTypeId)
                SELECT Id, ActivitySessionTypeId FROM Activities
                WHERE ActivitySessionTypeId IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivitySessionTags");
        }
    }
}
