namespace Callahan.Api.DTOs;

public record StreakDto(string Type, string Label, int CurrentWeeks, int BestWeeks);

public record StreakWeekDto(DateOnly WeekStart, bool Qualifies, List<WorkoutSessionSummaryDto> Workouts, List<ActivityDto> Runs);

public record StreakDetailDto(string Type, string Label, List<StreakWeekDto> Weeks);
