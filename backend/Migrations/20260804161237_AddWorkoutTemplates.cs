using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkoutTemplateId",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkoutTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplateExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkoutTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetSets = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetReps = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplateExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExercises_WorkoutTemplates_WorkoutTemplateId",
                        column: x => x.WorkoutTemplateId,
                        principalTable: "WorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Exercises",
                columns: new[] { "Id", "Category", "Name" },
                values: new object[,]
                {
                    { 11, 1, "Trap Bar Deadlift" },
                    { 12, 2, "Bulgarian Split Squat (DB)" },
                    { 13, 2, "Single Leg Hamstring Curl" },
                    { 14, 0, "Incline DB Press" },
                    { 15, 1, "Cable Row" },
                    { 16, 0, "DB Lateral Raise" },
                    { 17, 1, "Cable Face Pull" },
                    { 18, 2, "Single Leg Standing Calf Raise" },
                    { 19, 2, "Barbell or Trap Bar Squat" },
                    { 20, 2, "Single Leg RDL (DB)" },
                    { 21, 0, "Push Press" },
                    { 22, 1, "Chin-Ups" },
                    { 23, 3, "Pallof Press" },
                    { 24, 2, "Box Jump" }
                });

            migrationBuilder.InsertData(
                table: "WorkoutTemplates",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Workout 1 — Lower & Power", 1 },
                    { 2, "Workout 2 — Upper", 2 },
                    { 3, "Workout 3 — Full Body Athletic", 3 }
                });

            migrationBuilder.InsertData(
                table: "WorkoutTemplateExercises",
                columns: new[] { "Id", "ExerciseId", "ExerciseOrder", "TargetReps", "TargetSets", "WorkoutTemplateId" },
                values: new object[,]
                {
                    { 1, 11, 1, "5-6", 4, 1 },
                    { 2, 12, 2, "8/leg", 3, 1 },
                    { 3, 8, 3, "10-12", 3, 1 },
                    { 4, 13, 4, "8-10/leg", 3, 1 },
                    { 5, 14, 1, "8", 4, 2 },
                    { 6, 5, 2, "6-8", 4, 2 },
                    { 7, 15, 3, "10", 3, 2 },
                    { 8, 16, 4, "12", 3, 2 },
                    { 9, 17, 5, "15", 3, 2 },
                    { 10, 18, 6, "15-20/leg", 3, 2 },
                    { 11, 19, 1, "6-8", 4, 3 },
                    { 12, 20, 2, "8/leg", 3, 3 },
                    { 13, 21, 3, "6", 3, 3 },
                    { 14, 22, 4, "AMRAP", 3, 3 },
                    { 15, 23, 5, "12/side", 3, 3 },
                    { 16, 24, 6, "5", 3, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_WorkoutTemplateId",
                table: "WorkoutSessions",
                column: "WorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_ExerciseId",
                table: "WorkoutTemplateExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_WorkoutTemplateId",
                table: "WorkoutTemplateExercises",
                column: "WorkoutTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutSessions_WorkoutTemplates_WorkoutTemplateId",
                table: "WorkoutSessions",
                column: "WorkoutTemplateId",
                principalTable: "WorkoutTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutSessions_WorkoutTemplates_WorkoutTemplateId",
                table: "WorkoutSessions");

            migrationBuilder.DropTable(
                name: "WorkoutTemplateExercises");

            migrationBuilder.DropTable(
                name: "WorkoutTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_WorkoutTemplateId",
                table: "WorkoutSessions");

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Exercises",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DropColumn(
                name: "WorkoutTemplateId",
                table: "WorkoutSessions");
        }
    }
}
