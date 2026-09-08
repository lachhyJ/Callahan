namespace Callahan.Api.DTOs;

public record FinisherDto(int ExerciseId, string ExerciseName, int TargetSets, string TargetReps, int RestSeconds, bool IsAssisted, bool IsTimeBased, bool IsPerSide, int PerSideDelaySeconds, List<PreviousSetDto> PreviousSets);
