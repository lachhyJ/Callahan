namespace Callahan.Api.DTOs;

public record TrendPointDto(DateOnly MonthStart, decimal VolumeKg, int GymSessions, int RunSessions);
