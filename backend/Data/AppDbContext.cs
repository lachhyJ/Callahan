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
            new Exercise { Id = 10, Name = "Plank", Category = ExerciseCategory.Core }
        );
    }
}
