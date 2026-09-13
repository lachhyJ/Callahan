namespace Callahan.Api.DTOs;

public record RoutineItemDto(
    int Id,
    int ItemOrder,
    string Name,
    string Prescription,
    string? Cue,
    int? HoldSeconds,
    bool IsPerSide,
    int? PerSideDelaySeconds);

public record RoutineCompletionDto(int Id, DateOnly Date, string? Notes);

public record RoutineDto(
    int Id,
    string Name,
    string Purpose,
    string Cadence,
    string Notes,
    List<RoutineItemDto> Items,
    // Today's completion, if there is one - what the page's tick reads from.
    RoutineCompletionDto? Today,
    // Recent history, newest first, for the "last done" line and the notes list.
    List<RoutineCompletionDto> Recent);

public record MarkRoutineDoneRequest(DateOnly? Date, string? Notes);
