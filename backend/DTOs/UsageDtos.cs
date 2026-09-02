namespace Callahan.Api.DTOs;

// Sent in batches: the client buffers events and flushes on navigation and on
// page-hide, so one request can carry several.
//
// AgeMs rather than an absolute timestamp — how long before the flush the event
// happened. The server turns that into a time using its own clock, so a device
// with a wrong clock can't write rows dated to next year.
public record UsageEventDto(
    string Kind,
    string Path,
    string? FromPath,
    int? DwellMs,
    string? Action,
    string? Detail,
    long AgeMs
);

public record UsageBatchRequest(List<UsageEventDto> Events);
