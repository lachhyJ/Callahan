namespace Callahan.Api.DTOs;

public record RunningSessionDto(int Id, DateOnly Date, decimal DistanceKm, int DurationSeconds, string? Notes);

public record CreateRunningSessionRequest(DateOnly Date, decimal DistanceKm, int DurationSeconds, string? Notes);
