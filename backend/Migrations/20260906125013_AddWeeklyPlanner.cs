using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DefaultDayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    IsOptional = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorkoutTemplateId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActivitySessionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    RoutineId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanSlots_ActivitySessionTypes_ActivitySessionTypeId",
                        column: x => x.ActivitySessionTypeId,
                        principalTable: "ActivitySessionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanSlots_Routines_RoutineId",
                        column: x => x.RoutineId,
                        principalTable: "Routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanSlots_WorkoutTemplates_WorkoutTemplateId",
                        column: x => x.WorkoutTemplateId,
                        principalTable: "WorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanSlotWeeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlanSlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanSlotWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanSlotWeeks_PlanSlots_PlanSlotId",
                        column: x => x.PlanSlotId,
                        principalTable: "PlanSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ActivitySessionTypes",
                columns: new[] { "Id", "ActivityType", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 9, 0, "Field 1 - Acceleration & Jump Quality", 4 },
                    { 10, 0, "Field 2 - Repeat Effort & COD", 5 }
                });

            // The program's default week. Seeded here rather than in HasData
            // because the slots point at the Gym 1/2/3 templates, which are
            // themselves migration-seeded - see AddProgramV3Templates. Kind:
            // 0 Gym, 1 Field, 2 Routine, 3 Aerobic, 4 Rest. Day 0 = Monday.
            //
            // The gym sessions are numbered for week order, not priority. The
            // spacing is the point: Gym 2 is deliberately light on legs and
            // sits the day before Field 2, and nothing heavy lands in front of
            // a field session.
            migrationBuilder.InsertData(
                table: "PlanSlots",
                columns: new[] { "Id", "DefaultDayOfWeek", "SlotOrder", "Kind", "Label", "IsOptional", "WorkoutTemplateId", "ActivitySessionTypeId", "RoutineId" },
                values: new object[,]
                {
                    { 1, 0, 1, 1, "Field 1 - Acceleration & Jumps",   false, null, 9,    null },
                    { 2, 1, 1, 2, "Jump Block",                       false, null, null, 2 },
                    { 3, 1, 2, 0, "Gym 1",                            false, 4,    null, null },
                    { 4, 2, 1, 4, "Rest / throws",                    true,  null, null, null },
                    { 5, 3, 1, 0, "Gym 2",                            false, 5,    null, null },
                    { 6, 4, 1, 1, "Field 2 - Repeat Effort & COD",    false, null, 10,   null },
                    { 7, 5, 1, 2, "Jump Block",                       false, null, null, 2 },
                    { 8, 5, 2, 0, "Gym 3",                            false, 6,    null, null },
                    { 9, 6, 1, 3, "Aerobic - easy ride, or rest",     true,  null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanSlots_ActivitySessionTypeId",
                table: "PlanSlots",
                column: "ActivitySessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanSlots_RoutineId",
                table: "PlanSlots",
                column: "RoutineId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanSlots_WorkoutTemplateId",
                table: "PlanSlots",
                column: "WorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanSlotWeeks_PlanSlotId_WeekStart",
                table: "PlanSlotWeeks",
                columns: new[] { "PlanSlotId", "WeekStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PlanSlots WHERE Id BETWEEN 1 AND 9;");

            migrationBuilder.DropTable(
                name: "PlanSlotWeeks");

            migrationBuilder.DropTable(
                name: "PlanSlots");

            migrationBuilder.DeleteData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "ActivitySessionTypes",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}
