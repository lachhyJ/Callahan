using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Callahan.Api.Migrations
{
    /// <summary>
    /// The Ultimate program v3 rebuild: Day A/B/C are replaced by Gym 1/2/3.
    ///
    /// Hand-written rather than scaffolded from HasData, deliberately. HasData
    /// describes the seed state of a brand-new database, and this database
    /// stopped being that when the Hevy import landed - production carries ~40
    /// exercises the seed block has never known about, and three template
    /// columns (RestSeconds, Tempo, Cue) that the running app writes to. Making
    /// HasData describe the current program would put those runtime-owned
    /// columns under seed management, and a later unrelated migration would then
    /// silently reset every rest duration tuned in the app. So the seed block
    /// stays a record of initial state and forward changes live in migrations.
    ///
    /// Ids are hard-coded above production's current ceilings (exercises 92,
    /// template-exercises 16, muscle targets 135) because EF's model snapshot
    /// stops far below them - anything derived from the snapshot would collide
    /// with rows that already exist.
    ///
    /// The old templates are retired, never deleted: WorkoutSessions reference
    /// them by WorkoutTemplateId, and History, Streaks and the monthly reports
    /// all read their names.
    /// </summary>
    public partial class AddProgramV3Templates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Retire Day A/B/C -------------------------------------------------
            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 1,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day A (v1)", 10, true });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 2,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day B (v1)", 11, true });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 3,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day C (v1)", 12, true });

            // --- Backfill the reused import-only exercises ------------------------
            // Gym 1/2 reuse three exercises that reached production through the
            // Hevy import, not through HasData: 54 Hip Thrust (Machine), 67 Pogo
            // Jumps, 75 Seated Calf Raise. A database seeded only from HasData
            // (local dev, a fresh host) has exercises 1-30 and none of these, so
            // the template slots below would fail their foreign key there while
            // succeeding in production - the worst shape a migration bug can
            // take. OR IGNORE makes this a no-op against production, where all
            // three already exist, and a backfill everywhere else.
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO Exercises (Id, Name, Category, IsAssisted) VALUES
                    (54, 'Hip Thrust (Machine)', 2, 0),
                    (67, 'Pogo Jumps',           4, 0),
                    (75, 'Seated Calf Raise',    2, 0);");

            // Their muscle targets, matched to production. Guarded on the
            // exercise rather than on an Id, since these rows' production Ids
            // sit inside the unmanaged 1-135 range and hard-coding them could
            // collide with unrelated rows.
            migrationBuilder.Sql(@"
                INSERT INTO ExerciseMuscleTargets (Id, ExerciseId, MuscleGroup, IsPrimary)
                SELECT 141, 54, 7, 1 WHERE NOT EXISTS (SELECT 1 FROM ExerciseMuscleTargets WHERE ExerciseId = 54);
                INSERT INTO ExerciseMuscleTargets (Id, ExerciseId, MuscleGroup, IsPrimary)
                SELECT 142, 54, 6, 0 WHERE NOT EXISTS (SELECT 1 FROM ExerciseMuscleTargets WHERE ExerciseId = 54 AND MuscleGroup = 6);
                INSERT INTO ExerciseMuscleTargets (Id, ExerciseId, MuscleGroup, IsPrimary)
                SELECT 143, 67, 8, 1 WHERE NOT EXISTS (SELECT 1 FROM ExerciseMuscleTargets WHERE ExerciseId = 67);
                INSERT INTO ExerciseMuscleTargets (Id, ExerciseId, MuscleGroup, IsPrimary)
                SELECT 144, 75, 8, 1 WHERE NOT EXISTS (SELECT 1 FROM ExerciseMuscleTargets WHERE ExerciseId = 75);");

            // --- New exercises ----------------------------------------------------
            // Category is the app's Push/Pull/Legs/Core/Cardio/Other axis
            // (0/1/2/3/4/5).
            migrationBuilder.InsertData(
                table: "Exercises",
                columns: new[] { "Id", "Name", "Category", "IsAssisted" },
                values: new object[,]
                {
                    { 93, "Copenhagen Plank", 3, false },
                    { 94, "Tibialis Raise", 2, false },
                    { 95, "Skater Bounds", 2, false },
                    { 96, "Lateral Lunge", 2, false },
                    { 97, "Nordic Curl", 2, false },
                    { 98, "Single-Leg Balance + Reach (Y-Pattern)", 2, false }
                });

            // Muscle targets, for the Muscle Balance page. MuscleGroup is
            // Chest/Back/Shoulders/Biceps/Triceps/Quads/Hamstrings/Glutes/
            // Calves/Core (0-9).
            //
            // Copenhagen Plank (93), Tibialis Raise (94) and Y-Pattern Balance
            // (98) deliberately get no rows: the enum has no adductor, tibialis
            // or ankle member, and force-fitting them would put wrong data into
            // a page that exists to measure push/pull/leg distribution. Absent
            // is more honest than approximately wrong.
            migrationBuilder.InsertData(
                table: "ExerciseMuscleTargets",
                columns: new[] { "Id", "ExerciseId", "MuscleGroup", "IsPrimary" },
                values: new object[,]
                {
                    { 136, 95, 7, true },   // Skater Bounds  - Glutes
                    { 137, 95, 5, false },  //                - Quads
                    { 138, 96, 7, true },   // Lateral Lunge  - Glutes
                    { 139, 96, 5, false },  //                - Quads
                    { 140, 97, 6, true }    // Nordic Curl    - Hamstrings
                });

            // --- The three new templates -----------------------------------------
            // SortOrder is week order (the spacing that keeps a heavy leg day
            // off the front of a field session). PriorityOrder is which to keep
            // when the week is short - deliberately different: Gym 1, Gym 3,
            // Gym 2. LowerBodyLoad: 0 None, 1 Light, 2 Heavy.
            migrationBuilder.InsertData(
                table: "WorkoutTemplates",
                columns: new[] { "Id", "Name", "Subtitle", "SortOrder", "IsRetired", "LowerBodyLoad", "PriorityOrder" },
                values: new object[,]
                {
                    { 4, "Gym 1", "Lower Strength & Ankle",   1, false, 2, 1 },
                    { 5, "Gym 2", "Upper & Light Unilateral", 2, false, 1, 3 },
                    { 6, "Gym 3", "Squat & Posterior Chain",  3, false, 2, 2 }
                });

            // --- Template slots ---------------------------------------------------
            // Tempo is the program's two settings only: "2:0:X" controlled,
            // "X:0:X" explosive. Null where a hold or a balance drill makes
            // tempo meaningless.
            migrationBuilder.InsertData(
                table: "WorkoutTemplateExercises",
                columns: new[] { "Id", "WorkoutTemplateId", "ExerciseId", "ExerciseOrder", "TargetSets", "WarmupSets", "TargetReps", "RestSeconds", "Tempo", "Cue" },
                values: new object[,]
                {
                    // Gym 1 - Lower Strength & Ankle
                    { 17, 4, 67, 1, 3, 0, "12",          120, "X:0:X", "Stiff ankles, minimal ground contact time" },
                    { 18, 4, 11, 2, 4, 1, "4",           150, "X:0:X", "Your best lift. Explosive up." },
                    { 19, 4, 12, 3, 4, 0, "8/leg",       150, "2:0:X", "Your strongest unilateral lift" },
                    { 20, 4, 54, 4, 3, 0, "8",            90, "2:0:X", null },
                    { 21, 4,  5, 5, 5, 0, "3",            90, "2:0:X", "Five sets of three, never to failure" },
                    { 22, 4, 93, 6, 3, 0, "20 secs/side", 60, null,    "Short-lever to start, progress to full" },
                    { 23, 4, 18, 7, 3, 0, "12",           60, "2:0:X", "Full heel drop, pause at bottom" },
                    { 24, 4, 94, 8, 2, 0, "20",           60, "2:0:X", null },

                    // Gym 2 - Upper & Light Unilateral. Deliberately light on
                    // legs: it sits the day before Field 2.
                    { 25, 5, 95, 1, 3, 0, "6/side",      120, "X:0:X", "Land, stick 1 sec, then go" },
                    { 26, 5, 20, 2, 3, 0, "8/leg",        90, "2:0:X", "Contralateral hold. Moderate load, not a grinder." },
                    { 27, 5, 14, 3, 4, 1, "8",            90, "2:0:X", null },
                    { 28, 5, 15, 4, 3, 0, "10",           90, "2:0:X", "Cut this first if time-limited" },
                    { 29, 5, 17, 5, 3, 0, "15",           60, "2:0:X", "Shoulder health for layouts" },
                    { 30, 5, 75, 6, 3, 0, "15",           60, "2:0:X", "Soleus - different job to straight-knee" },
                    { 31, 5, 23, 7, 3, 0, "12/side",      60, "2:0:X", "Cut this first if time-limited" },

                    // Gym 3 - Squat & Posterior Chain
                    { 32, 6, 19, 1, 4, 1, "6",           150, "2:0:X", "Your weakest lift with the most headroom - it gets a fresh slot" },
                    { 33, 6, 96, 2, 3, 0, "8/side",       90, "2:0:X", "Frontal plane" },
                    { 34, 6, 97, 3, 3, 0, "5",           120, "2:0:X", "Slow lower, assisted up" },
                    { 35, 6, 21, 4, 3, 0, "5",            90, "X:0:X", "Light, explosive, no grinding" },
                    { 36, 6, 22, 5, 3, 0, "3-4",          90, "2:0:X", "Submaximal" },
                    { 37, 6, 28, 6, 3, 0, "8",            60, "2:0:X", null },
                    { 38, 6, 98, 7, 3, 0, "5/direction/side", 60, null, null }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM WorkoutTemplateExercises WHERE Id BETWEEN 17 AND 38;");
            migrationBuilder.Sql("DELETE FROM WorkoutTemplates WHERE Id IN (4, 5, 6);");
            migrationBuilder.Sql("DELETE FROM ExerciseMuscleTargets WHERE Id BETWEEN 136 AND 140;");
            migrationBuilder.Sql("DELETE FROM Exercises WHERE Id BETWEEN 93 AND 98;");
            // Only the rows this migration could have created; 54/67/75 are left
            // alone, since OR IGNORE means we cannot tell whether we inserted them.
            migrationBuilder.Sql("DELETE FROM ExerciseMuscleTargets WHERE Id BETWEEN 141 AND 144;");

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 1,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day A", 1, false });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 2,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day B", 2, false });

            migrationBuilder.UpdateData(
                table: "WorkoutTemplates", keyColumn: "Id", keyValue: 3,
                columns: new[] { "Name", "SortOrder", "IsRetired" },
                values: new object[] { "Day C", 3, false });
        }
    }
}
