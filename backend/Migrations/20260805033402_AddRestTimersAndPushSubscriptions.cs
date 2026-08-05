using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRestTimersAndPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "WorkoutTemplateExercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "Finishers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", nullable: false),
                    Auth = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 1,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 2,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 3,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 4,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 5,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "Finishers",
                keyColumn: "Id",
                keyValue: 6,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 1,
                column: "RestSeconds",
                value: 150);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 2,
                column: "RestSeconds",
                value: 150);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 3,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 4,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 5,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 6,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 7,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 8,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 9,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 10,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 11,
                column: "RestSeconds",
                value: 150);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 12,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 13,
                column: "RestSeconds",
                value: 150);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 14,
                column: "RestSeconds",
                value: 90);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 15,
                column: "RestSeconds",
                value: 60);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplateExercises",
                keyColumn: "Id",
                keyValue: 16,
                column: "RestSeconds",
                value: 105);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "WorkoutTemplateExercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "Finishers");
        }
    }
}
