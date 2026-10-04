namespace Callahan.Api.DTOs;

// What the iOS home screen widget gets: the status code and the load figures
// behind it, nothing else. Labels and colours stay client-side, as on the web.
public record WidgetTrainingStatusDto(
    DateOnly Date,
    int Code,
    double? AcwrRatio,
    int? AcuteLoad,
    int? ChronicLoad);
