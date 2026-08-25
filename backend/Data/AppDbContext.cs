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
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<WorkoutTemplateExercise> WorkoutTemplateExercises => Set<WorkoutTemplateExercise>();
    public DbSet<ExerciseNote> ExerciseNotes => Set<ExerciseNote>();
    public DbSet<Finisher> Finishers => Set<Finisher>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<ExerciseMuscleTarget> ExerciseMuscleTargets => Set<ExerciseMuscleTarget>();
    public DbSet<ActivitySessionType> ActivitySessionTypes => Set<ActivitySessionType>();
    public DbSet<TaperEvent> TaperEvents => Set<TaperEvent>();
    public DbSet<TaperCheckIn> TaperCheckIns => Set<TaperCheckIn>();
    public DbSet<TaperReminderLog> TaperReminderLogs => Set<TaperReminderLog>();
    public DbSet<MonthlyReport> MonthlyReports => Set<MonthlyReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Soft delete: everywhere in the app queries through these DbSets (or
        // through a navigation/join to them) automatically excludes anything
        // pending its 7-day recovery window, with no per-query .Where() needed.
        // Listing/purging deleted rows explicitly opts out via IgnoreQueryFilters().
        modelBuilder.Entity<WorkoutSession>().HasQueryFilter(s => s.DeletedAt == null);
        modelBuilder.Entity<Activity>().HasQueryFilter(a => a.DeletedAt == null);

        modelBuilder.Entity<Activity>()
            .HasIndex(a => a.GarminActivityId)
            .IsUnique()
            .HasFilter("[GarminActivityId] IS NOT NULL");

        modelBuilder.Entity<TaperCheckIn>()
            .HasIndex(c => new { c.TaperEventId, c.Date })
            .IsUnique();

        modelBuilder.Entity<TaperReminderLog>()
            .HasIndex(r => new { r.TaperEventId, r.Date })
            .IsUnique();

        modelBuilder.Entity<MonthlyReport>()
            .HasIndex(r => new { r.Year, r.Month })
            .IsUnique();

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
            new Exercise { Id = 24, Name = "Box Jump", Category = ExerciseCategory.Legs },

            // Optional finishers
            new Exercise { Id = 25, Name = "Dead Bug", Category = ExerciseCategory.Core },
            new Exercise { Id = 26, Name = "Cable Woodchop", Category = ExerciseCategory.Core },
            new Exercise { Id = 27, Name = "Hanging Knee Raise", Category = ExerciseCategory.Core },
            new Exercise { Id = 28, Name = "Ab Wheel Rollout", Category = ExerciseCategory.Core },
            new Exercise { Id = 29, Name = "Cable Crunch", Category = ExerciseCategory.Core },
            new Exercise { Id = 30, Name = "Side Plank with Reach", Category = ExerciseCategory.Core }
        );

        modelBuilder.Entity<WorkoutTemplate>().HasData(
            new WorkoutTemplate { Id = 1, Name = "Day A", Subtitle = "Lower & Power", SortOrder = 1 },
            new WorkoutTemplate { Id = 2, Name = "Day B", Subtitle = "Upper", SortOrder = 2 },
            new WorkoutTemplate { Id = 3, Name = "Day C", Subtitle = "Full Body Athletic", SortOrder = 3 }
        );

        // Rest durations per the program's Rest Guidelines table:
        // Heavy compounds (deadlift/squat/split squat) 150s, Accessory work 90s,
        // Isolation/light/core 60s, Jumps & plyos 105s. Exercises the table didn't
        // name explicitly are judgment calls — adjustable in the UI regardless.
        modelBuilder.Entity<WorkoutTemplateExercise>().HasData(
            // Day A: Lower & Power
            new WorkoutTemplateExercise { Id = 1, WorkoutTemplateId = 1, ExerciseId = 11, ExerciseOrder = 1, TargetSets = 4, WarmupSets = 1, TargetReps = "5-6", RestSeconds = 150 },
            new WorkoutTemplateExercise { Id = 2, WorkoutTemplateId = 1, ExerciseId = 12, ExerciseOrder = 2, TargetSets = 3, TargetReps = "8/leg", RestSeconds = 150 },
            new WorkoutTemplateExercise { Id = 3, WorkoutTemplateId = 1, ExerciseId = 8, ExerciseOrder = 3, TargetSets = 3, TargetReps = "10-12", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 4, WorkoutTemplateId = 1, ExerciseId = 13, ExerciseOrder = 4, TargetSets = 3, TargetReps = "8-10/leg", RestSeconds = 60 },

            // Day B: Upper
            new WorkoutTemplateExercise { Id = 5, WorkoutTemplateId = 2, ExerciseId = 14, ExerciseOrder = 1, TargetSets = 4, WarmupSets = 1, TargetReps = "8", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 6, WorkoutTemplateId = 2, ExerciseId = 5, ExerciseOrder = 2, TargetSets = 4, TargetReps = "6-8", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 7, WorkoutTemplateId = 2, ExerciseId = 15, ExerciseOrder = 3, TargetSets = 3, TargetReps = "10", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 8, WorkoutTemplateId = 2, ExerciseId = 16, ExerciseOrder = 4, TargetSets = 3, TargetReps = "12", RestSeconds = 60 },
            new WorkoutTemplateExercise { Id = 9, WorkoutTemplateId = 2, ExerciseId = 17, ExerciseOrder = 5, TargetSets = 3, TargetReps = "15", RestSeconds = 60 },
            new WorkoutTemplateExercise { Id = 10, WorkoutTemplateId = 2, ExerciseId = 18, ExerciseOrder = 6, TargetSets = 3, TargetReps = "15-20/leg", RestSeconds = 60 },

            // Day C: Full Body Athletic
            new WorkoutTemplateExercise { Id = 11, WorkoutTemplateId = 3, ExerciseId = 19, ExerciseOrder = 1, TargetSets = 4, WarmupSets = 1, TargetReps = "6-8", RestSeconds = 150 },
            new WorkoutTemplateExercise { Id = 12, WorkoutTemplateId = 3, ExerciseId = 20, ExerciseOrder = 2, TargetSets = 3, TargetReps = "8/leg", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 13, WorkoutTemplateId = 3, ExerciseId = 21, ExerciseOrder = 3, TargetSets = 3, TargetReps = "6", RestSeconds = 150 },
            new WorkoutTemplateExercise { Id = 14, WorkoutTemplateId = 3, ExerciseId = 22, ExerciseOrder = 4, TargetSets = 3, TargetReps = "AMRAP", RestSeconds = 90 },
            new WorkoutTemplateExercise { Id = 15, WorkoutTemplateId = 3, ExerciseId = 23, ExerciseOrder = 5, TargetSets = 3, TargetReps = "12/side", RestSeconds = 60 },
            new WorkoutTemplateExercise { Id = 16, WorkoutTemplateId = 3, ExerciseId = 24, ExerciseOrder = 6, TargetSets = 3, TargetReps = "5", RestSeconds = 105 }
        );

        // Session classification, shared by Running and Ultimate (an
        // ActivityType discriminator, not two parallel tables — see
        // moxie-vault/30-projects/callahan/decisions.md for why). Running's
        // rows are the Ultimate Athlete Training Program's Run 1/2/3, in the
        // program's own priority order (Run 1 highest — most
        // frisbee-specific — if a week only fits two sessions). Ultimate's
        // rows are the kinds of session Lachlan actually plays.
        modelBuilder.Entity<ActivitySessionType>().HasData(
            new ActivitySessionType { Id = 1, Name = "High Speed Intervals", ActivityType = ActivityType.Running, SortOrder = 1 },
            new ActivitySessionType { Id = 2, Name = "Speed & Acceleration", ActivityType = ActivityType.Running, SortOrder = 2 },
            new ActivitySessionType { Id = 3, Name = "Easy Aerobic Run", ActivityType = ActivityType.Running, SortOrder = 3 },
            new ActivitySessionType { Id = 4, Name = "Solo", ActivityType = ActivityType.Ultimate, SortOrder = 1 },
            new ActivitySessionType { Id = 5, Name = "Throws", ActivityType = ActivityType.Ultimate, SortOrder = 2 },
            new ActivitySessionType { Id = 6, Name = "Pod", ActivityType = ActivityType.Ultimate, SortOrder = 3 },
            new ActivitySessionType { Id = 7, Name = "Club Training", ActivityType = ActivityType.Ultimate, SortOrder = 4 },
            new ActivitySessionType { Id = 8, Name = "Game", ActivityType = ActivityType.Ultimate, SortOrder = 5 }
        );

        modelBuilder.Entity<Finisher>().HasData(
            new Finisher { Id = 1, ExerciseId = 25, SortOrder = 1, TargetSets = 3, TargetReps = "8/side", RestSeconds = 60 },
            new Finisher { Id = 2, ExerciseId = 26, SortOrder = 2, TargetSets = 3, TargetReps = "12/side", RestSeconds = 60 },
            new Finisher { Id = 3, ExerciseId = 27, SortOrder = 3, TargetSets = 3, TargetReps = "12", RestSeconds = 60 },
            new Finisher { Id = 4, ExerciseId = 28, SortOrder = 4, TargetSets = 3, TargetReps = "8-10", RestSeconds = 60 },
            new Finisher { Id = 5, ExerciseId = 29, SortOrder = 5, TargetSets = 3, TargetReps = "15", RestSeconds = 60 },
            new Finisher { Id = 6, ExerciseId = 30, SortOrder = 6, TargetSets = 3, TargetReps = "30 secs/side", RestSeconds = 60 }
        );
    }
}
