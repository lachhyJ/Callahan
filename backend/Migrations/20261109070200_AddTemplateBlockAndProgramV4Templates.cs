using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callahan.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateBlockAndProgramV4Templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Block",
                table: "WorkoutTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "Block",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 2,
                column: "Block",
                value: 0);

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates",
                keyColumn: "Id",
                keyValue: 3,
                column: "Block",
                value: 0);

            // --- Program v4 data --------------------------------------------------
            // Hand-written SQL for the same reason AddProgramV3Templates is (HasData does not
            // describe production's templates). Tags the three existing templates as the
            // no-club block, adds Gym A and Gym B for the in-season block, and applies the
            // v4 no-club edits. Slots are only ever removed, never exercises: set history
            // references exercises, not slots.

            // Gym B holds exercise 92 (the curl), which reached production through the Hevy
            // import: a database built only from migrations has Nordic Curl (97) in its place and
            // no 92, so the slot below would fail its foreign key there while succeeding in
            // production. OR IGNORE is a no-op against production and a backfill everywhere else.
            migrationBuilder.Sql(@"
INSERT OR IGNORE INTO Exercises (Id, Name, Category, IsAssisted, ShortName)
VALUES (92, 'Single Leg Hamstring Curl (Alternating)', 2, 0, '1-Leg Hamstring Curl');
INSERT INTO ExerciseMuscleTargets (Id, ExerciseId, MuscleGroup, IsPrimary)
SELECT 145, 92, 6, 1 WHERE NOT EXISTS (SELECT 1 FROM ExerciseMuscleTargets WHERE ExerciseId = 92);");

            migrationBuilder.Sql("UPDATE WorkoutTemplates SET Block = 2 WHERE Id IN (4, 5, 6);");

            migrationBuilder.Sql(@"
INSERT INTO WorkoutTemplates (Id, Name, Subtitle, SortOrder, IsRetired, LowerBodyLoad, PriorityOrder, Block)
SELECT 7, 'Gym A', 'Lower & Push', 1, 0, 2, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 7);

INSERT INTO WorkoutTemplates (Id, Name, Subtitle, SortOrder, IsRetired, LowerBodyLoad, PriorityOrder, Block)
SELECT 8, 'Gym B', 'Squat, Posterior Chain & Pull', 2, 0, 2, 2, 1
WHERE NOT EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 8);");

            // Rest, tempo, cue and the timed-hold target are copied from the slot that holds the
            // exercise today (templates 4-6; every exercise below sits in exactly one of them):
            // those values are tuned in the app, not authored in the program doc. Only what v4
            // changes is overridden. Columns: exercise, order, sets, reps, min, max, rest
            // override, warm-up sets, superset-with-next, superset rest (stored on a group's
            // first slot).
            migrationBuilder.Sql(@"
WITH v(ex, ord, sets, reps, mn, mx, rest, wu, ss, ssrest) AS (VALUES
    (67, 0, 2, '20',            20,   20,   45,   0, 0, NULL),
    (11, 1, 4, '4',              4,    4, NULL,   1, 0, NULL),
    (12, 2, 3, '8/leg',          8,    8, NULL,   0, 0, NULL),
    (14, 3, 3, '8',              8,    8, NULL,   1, 1, 90),
    ( 5, 4, 4, '3',              3,    3, NULL,   0, 0, NULL),
    (18, 5, 3, '12',            12,   12, NULL,   0, 1, 60),
    (93, 6, 2, '20 secs/side', NULL, NULL, NULL,  0, 0, NULL))
INSERT INTO WorkoutTemplateExercises
    (WorkoutTemplateId, ExerciseId, ExerciseOrder, TargetSets, TargetReps, TargetRepsMin, TargetRepsMax,
     RestSeconds, Tempo, Cue, WarmupSets, TargetDurationSeconds, SupersetWithNext, SupersetRestSeconds)
SELECT 7, v.ex, v.ord, v.sets, v.reps, v.mn, v.mx,
       COALESCE(v.rest, src.RestSeconds, 0), src.Tempo, src.Cue, v.wu, src.TargetDurationSeconds, v.ss, v.ssrest
FROM v
LEFT JOIN WorkoutTemplateExercises src ON src.ExerciseId = v.ex AND src.WorkoutTemplateId IN (4, 5, 6)
WHERE EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 7)
  AND NOT EXISTS (SELECT 1 FROM WorkoutTemplateExercises WHERE WorkoutTemplateId = 7);");

            migrationBuilder.Sql(@"
WITH v(ex, ord, sets, reps, mn, mx, rest, wu, ss, ssrest) AS (VALUES
    (19, 0, 4, '6',     6,  6, NULL, 1, 0, NULL),
    (20, 1, 3, '8/leg', 8,  8, NULL, 0, 0, NULL),
    (96, 2, 2, '8/side',8,  8, NULL, 0, 0, NULL),
    (92, 3, 3, '6/leg', 6,  6, NULL, 0, 0, NULL),
    (15, 4, 3, '10',   10, 10, NULL, 0, 1, 90),
    (75, 5, 3, '15',   15, 15, NULL, 0, 0, NULL))
INSERT INTO WorkoutTemplateExercises
    (WorkoutTemplateId, ExerciseId, ExerciseOrder, TargetSets, TargetReps, TargetRepsMin, TargetRepsMax,
     RestSeconds, Tempo, Cue, WarmupSets, TargetDurationSeconds, SupersetWithNext, SupersetRestSeconds)
SELECT 8, v.ex, v.ord, v.sets, v.reps, v.mn, v.mx,
       COALESCE(v.rest, src.RestSeconds, 0), src.Tempo, src.Cue, v.wu, src.TargetDurationSeconds, v.ss, v.ssrest
FROM v
LEFT JOIN WorkoutTemplateExercises src ON src.ExerciseId = v.ex AND src.WorkoutTemplateId IN (4, 5, 6)
WHERE EXISTS (SELECT 1 FROM WorkoutTemplates WHERE Id = 8)
  AND NOT EXISTS (SELECT 1 FROM WorkoutTemplateExercises WHERE WorkoutTemplateId = 8);");

            // Cues that would be wrong where they are copied to: Gym 1's pull-up cue says five
            // sets, the row's 'cut this first' does not apply in Gym B, and v4 lowers the curl
            // over 3-4 seconds, not 4.
            migrationBuilder.Sql(@"
UPDATE WorkoutTemplateExercises SET Cue = 'Strict first, assistance as needed. Target: 4 unassisted sets. Never strap.'
 WHERE WorkoutTemplateId = 7 AND ExerciseId = 5;
UPDATE WorkoutTemplateExercises SET Cue = NULL WHERE WorkoutTemplateId = 8 AND ExerciseId = 15;
UPDATE WorkoutTemplateExercises SET Cue = REPLACE(Cue, '4 second lower', '3-4 second lower')
 WHERE WorkoutTemplateId IN (6, 8) AND ExerciseId = 92;");

            // --- No-club block edits (Gym 1/2/3) -----------------------------------
            // Gym 1: the rep range had drifted to 12/12 against a '20-25' target.
            migrationBuilder.Sql("UPDATE WorkoutTemplateExercises SET TargetRepsMin = 20, TargetRepsMax = 25 WHERE WorkoutTemplateId = 4 AND ExerciseId = 67;");

            // Gym 2: Skater Bounds moves to the Sunday primer / Field 2 (it has no logged sets);
            // order becomes RDL, Incline, Row, Face Pull, Seated Calf, Pallof; calf range 15-20.
            migrationBuilder.Sql(@"
DELETE FROM WorkoutTemplateExercises WHERE WorkoutTemplateId = 5 AND ExerciseId = 95;
UPDATE WorkoutTemplateExercises SET ExerciseOrder = ExerciseOrder - 2 WHERE WorkoutTemplateId = 5;
UPDATE WorkoutTemplateExercises SET TargetRepsMax = 20 WHERE WorkoutTemplateId = 5 AND ExerciseId = 75;");

            // Gym 3: Push Press, Ab Wheel and the Y-reach go (the reach is in the ankle circuit
            // now); the curl is 6/leg with a 3-4 s lower. Slots only: the exercises and their
            // logged sets stay.
            migrationBuilder.Sql(@"
DELETE FROM WorkoutTemplateExercises WHERE WorkoutTemplateId = 6 AND ExerciseId IN (21, 28, 98);
UPDATE WorkoutTemplateExercises SET ExerciseOrder = 0 WHERE WorkoutTemplateId = 6 AND ExerciseId = 19;
UPDATE WorkoutTemplateExercises SET ExerciseOrder = 1 WHERE WorkoutTemplateId = 6 AND ExerciseId = 96;
UPDATE WorkoutTemplateExercises SET ExerciseOrder = 2, TargetRepsMin = 6, TargetRepsMax = 6 WHERE WorkoutTemplateId = 6 AND ExerciseId = 92;
UPDATE WorkoutTemplateExercises SET ExerciseOrder = 3 WHERE WorkoutTemplateId = 6 AND ExerciseId = 22;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gym A/B go; the slot removals and edits made to Gym 1/2/3 are not restored.
            migrationBuilder.Sql("DELETE FROM WorkoutTemplateExercises WHERE WorkoutTemplateId IN (7, 8);");
            migrationBuilder.Sql("DELETE FROM WorkoutTemplates WHERE Id IN (7, 8);");

            migrationBuilder.DropColumn(
                name: "Block",
                table: "WorkoutTemplates");
        }
    }
}
