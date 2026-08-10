using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMuscleTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMuscleTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExerciseId = table.Column<int>(type: "INTEGER", nullable: false),
                    MuscleGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMuscleTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMuscleTargets_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMuscleTargets_ExerciseId",
                table: "ExerciseMuscleTargets",
                column: "ExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMuscleTargets");
        }
    }
}
