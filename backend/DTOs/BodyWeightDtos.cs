namespace Callahan.Api.DTOs;

public record BodyWeightDto(DateOnly Date, double WeightKg, string? Source);

// The sync sends the whole window in one request: one Garmin range call, one upsert.
public record UpsertBodyWeightsRequest(List<BodyWeightDto>? Entries);

public record UpsertBodyWeightsResultDto(int Created, int Updated, int Skipped);
