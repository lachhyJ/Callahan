namespace Callahan.Api.DTOs;

public record FinisherDto(int ExerciseId, string ExerciseName, int TargetSets, string TargetReps, List<PreviousSetDto> PreviousSets);
