namespace Callahan.Api.DTOs;

public record FinisherDto(int ExerciseId, string ExerciseName, int TargetSets, string TargetReps, int RestSeconds, List<PreviousSetDto> PreviousSets);
