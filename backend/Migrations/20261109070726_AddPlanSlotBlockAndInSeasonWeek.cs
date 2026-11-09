using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSlotBlockAndInSeasonWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Block",
                table: "PlanSlots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Program v4. The slots that exist today are exactly v4's no-club week (Mon Field 1,
            // Tue Gym 1, Wed rest/throws, Thu Gym 2, Fri Field 2, Sat Gym 3, Sun aerobic), so
            // they are tagged NoClub. The in-season week is added beside them: Mon Gym A, Tue
            // rest, Thu Field 1, Fri Gym B, Sat an optional pod. Club (Wed, Sun) is not a slot:
            // it comes from the calendar as an "other event", and the planner stays unaware of it.
            // Session type ids: 9 Field 1, 6 Pod (seed in AppDbContext).
            migrationBuilder.Sql("UPDATE PlanSlots SET Block = 2 WHERE Block = 0;");

            // v4 renamed Field 1 (Defence & Acceleration). Labels are what the Plan page shows; the
            // calendar matches on the part before " - ", so this does not change matching.
            migrationBuilder.Sql("UPDATE PlanSlots SET Label = 'Field 1 - Defence & Acceleration' WHERE ActivitySessionTypeId = 9 AND Block = 2;");

            // The pod slot is labelled plain "Pod" so a calendar event titled "Pod" matches it
            // (matching keys on the label); IsOptional is what makes it optional.

            migrationBuilder.Sql(@"
INSERT INTO PlanSlots (DefaultDayOfWeek, SlotOrder, Kind, Label, IsOptional, WorkoutTemplateId, ActivitySessionTypeId, RoutineId, Block)
SELECT 0, 1, 0, 'Gym A', 0, 7, NULL, NULL, 1
WHERE EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 7)
  AND NOT EXISTS (SELECT 1 FROM PlanSlots WHERE Block = 1);
INSERT INTO PlanSlots (DefaultDayOfWeek, SlotOrder, Kind, Label, IsOptional, WorkoutTemplateId, ActivitySessionTypeId, RoutineId, Block)
SELECT 1, 1, 4, 'Rest', 1, NULL, NULL, NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM PlanSlots WHERE Block = 1 AND Kind = 4);
INSERT INTO PlanSlots (DefaultDayOfWeek, SlotOrder, Kind, Label, IsOptional, WorkoutTemplateId, ActivitySessionTypeId, RoutineId, Block)
SELECT 3, 1, 1, 'Field 1 - Defence & Acceleration', 0, NULL, 9, NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM PlanSlots WHERE Block = 1 AND Kind = 1 AND IsOptional = 0);
INSERT INTO PlanSlots (DefaultDayOfWeek, SlotOrder, Kind, Label, IsOptional, WorkoutTemplateId, ActivitySessionTypeId, RoutineId, Block)
SELECT 4, 1, 0, 'Gym B', 0, 8, NULL, NULL, 1
WHERE EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 8)
  AND NOT EXISTS (SELECT 1 FROM PlanSlots WHERE Block = 1 AND Label = 'Gym B');
INSERT INTO PlanSlots (DefaultDayOfWeek, SlotOrder, Kind, Label, IsOptional, WorkoutTemplateId, ActivitySessionTypeId, RoutineId, Block)
SELECT 5, 1, 1, 'Pod', 1, NULL, 6, NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM PlanSlots WHERE Block = 1 AND Label = 'Pod');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PlanSlots WHERE Block = 1;");

            migrationBuilder.DropColumn(
                name: "Block",
                table: "PlanSlots");
        }
    }
}
