using Callahan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
    public DbSet<RunningSession> RunningSessions => Set<RunningSession>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises => Set<WorkoutTemplateExercise>();
    public DbSet<ExerciseNote> ExerciseNotes => Set<ExerciseNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exercise>().HasData(
            new Exercise { Id = 1, Name = "Bench Press", Category = ExerciseCategory.Push },
            new Exercise { Id = 2, Name = "Overhead Press", Category = ExerciseCategory.Push },
            new Exercise { Id = 3, Name = "Push-up", Category = ExerciseCategory.Push },
            new Exercise { Id = 4, Name = "Deadlift", Category = ExerciseCategory.Pull },
            new Exercise { Id = 5, Name = "Pull-up", Category = ExerciseCategory.Pull },
            new Exercise { Id = 6, Name = "Barbell Row", Category = ExerciseCategory.Pull },
            new Exercise { Id = 7, Name = "Squat", Category = ExerciseCategory.Legs },
            new Exercise { Id = 8, Name = "Leg Press", Category = ExerciseCategory.Legs },
            new Exercise { Id = 9, Name = "Lunges", Category = ExerciseCategory.Legs },
            new Exercise { Id = 10, Name = "Plank", Category = ExerciseCategory.Core },

            // Ultimate Athlete Training Program
            new Exercise { Id = 11, Name = "Trap Bar Deadlift", Category = ExerciseCategory.Pull },
            new Exercise { Id = 12, Name = "Bulgarian Split Squat (DB)", Category = ExerciseCategory.Legs },
            new Exercise { Id = 13, Name = "Single Leg Hamstring Curl", Category = ExerciseCategory.Legs },
            new Exercise { Id = 14, Name = "Incline DB Press", Category = ExerciseCategory.Push },
            new Exercise { Id = 15, Name = "Cable Row", Category = ExerciseCategory.Pull },
            new Exercise { Id = 16, Name = "DB Lateral Raise", Category = ExerciseCategory.Push },
            new Exercise { Id = 17, Name = "Cable Face Pull", Category = ExerciseCategory.Pull },
            new Exercise { Id = 18, Name = "Single Leg Standing Calf Raise", Category = ExerciseCategory.Legs },
            new Exercise { Id = 19, Name = "Barbell or Trap Bar Squat", Category = ExerciseCategory.Legs },
            new Exercise { Id = 20, Name = "Single Leg RDL (DB)", Category = ExerciseCategory.Legs },
            new Exercise { Id = 21, Name = "Push Press", Category = ExerciseCategory.Push },
            new Exercise { Id = 22, Name = "Chin-Ups", Category = ExerciseCategory.Pull },
            new Exercise { Id = 23, Name = "Pallof Press", Category = ExerciseCategory.Core },
            new Exercise { Id = 24, Name = "Box Jump", Category = ExerciseCategory.Legs }
        );

        modelBuilder.Entity<WorkoutTemplate>().HasData(
            new WorkoutTemplate { Id = 1, Name = "Workout 1 — Lower & Power", SortOrder = 1 },
            new WorkoutTemplate { Id = 2, Name = "Workout 2 — Upper", SortOrder = 2 },
            new WorkoutTemplate { Id = 3, Name = "Workout 3 — Full Body Athletic", SortOrder = 3 }
        );

        modelBuilder.Entity<WorkoutTemplateExercise>().HasData(
            // Workout 1 — Day A: Lower & Power
            new WorkoutTemplateExercise { Id = 1, WorkoutTemplateId = 1, ExerciseId = 11, ExerciseOrder = 1, TargetSets = 4, TargetReps = "5-6" },
            new WorkoutTemplateExercise { Id = 2, WorkoutTemplateId = 1, ExerciseId = 12, ExerciseOrder = 2, TargetSets = 3, TargetReps = "8/leg" },
            new WorkoutTemplateExercise { Id = 3, WorkoutTemplateId = 1, ExerciseId = 8, ExerciseOrder = 3, TargetSets = 3, TargetReps = "10-12" },
            new WorkoutTemplateExercise { Id = 4, WorkoutTemplateId = 1, ExerciseId = 13, ExerciseOrder = 4, TargetSets = 3, TargetReps = "8-10/leg" },

            // Workout 2 — Day B: Upper
            new WorkoutTemplateExercise { Id = 5, WorkoutTemplateId = 2, ExerciseId = 14, ExerciseOrder = 1, TargetSets = 4, TargetReps = "8" },
            new WorkoutTemplateExercise { Id = 6, WorkoutTemplateId = 2, ExerciseId = 5, ExerciseOrder = 2, TargetSets = 4, TargetReps = "6-8" },
            new WorkoutTemplateExercise { Id = 7, WorkoutTemplateId = 2, ExerciseId = 15, ExerciseOrder = 3, TargetSets = 3, TargetReps = "10" },
            new WorkoutTemplateExercise { Id = 8, WorkoutTemplateId = 2, ExerciseId = 16, ExerciseOrder = 4, TargetSets = 3, TargetReps = "12" },
            new WorkoutTemplateExercise { Id = 9, WorkoutTemplateId = 2, ExerciseId = 17, ExerciseOrder = 5, TargetSets = 3, TargetReps = "15" },
            new WorkoutTemplateExercise { Id = 10, WorkoutTemplateId = 2, ExerciseId = 18, ExerciseOrder = 6, TargetSets = 3, TargetReps = "15-20/leg" },

            // Workout 3 — Day C: Full Body Athletic
            new WorkoutTemplateExercise { Id = 11, WorkoutTemplateId = 3, ExerciseId = 19, ExerciseOrder = 1, TargetSets = 4, TargetReps = "6-8" },
            new WorkoutTemplateExercise { Id = 12, WorkoutTemplateId = 3, ExerciseId = 20, ExerciseOrder = 2, TargetSets = 3, TargetReps = "8/leg" },
            new WorkoutTemplateExercise { Id = 13, WorkoutTemplateId = 3, ExerciseId = 21, ExerciseOrder = 3, TargetSets = 3, TargetReps = "6" },
            new WorkoutTemplateExercise { Id = 14, WorkoutTemplateId = 3, ExerciseId = 22, ExerciseOrder = 4, TargetSets = 3, TargetReps = "AMRAP" },
            new WorkoutTemplateExercise { Id = 15, WorkoutTemplateId = 3, ExerciseId = 23, ExerciseOrder = 5, TargetSets = 3, TargetReps = "12/side" },
            new WorkoutTemplateExercise { Id = 16, WorkoutTemplateId = 3, ExerciseId = 24, ExerciseOrder = 6, TargetSets = 3, TargetReps = "5" }
        );
    }
}
