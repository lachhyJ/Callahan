namespace Callahan.Api.DTOs;

public record SeasonDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int? TargetTournamentId,
    string? TargetTournamentName,
    int TournamentCount);

public record CreateSeasonRequest(string Name, DateOnly StartDate, DateOnly EndDate, int? TargetTournamentId);

public record UpdateSeasonRequest(string Name, DateOnly StartDate, DateOnly EndDate, int? TargetTournamentId);

public record AttachTournamentsResponse(int Attached);
