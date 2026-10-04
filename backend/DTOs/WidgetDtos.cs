namespace Callahan.Api.DTOs;

// What the iOS home screen widget gets: the status code and the load figures
// behind it, nothing else. Labels and colours stay client-side, as on the web.
public record WidgetTrainingStatusDto(
    DateOnly Date,
    int Code,
    double? AcwrRatio,
    int? AcuteLoad,
    int? ChronicLoad);

// The Today widget: what is planned today, what is next, and how the week is going.
// `TrainingDay` comes from the server (3 am cutoff, the user-switchable zone) so the
// widget never works out "today" from the device clock.
public record WidgetSlotDto(string Label, string Kind, string State);

public record WidgetNextDto(string DayName, DateOnly Date, string Label, string Kind);

// State is one of Done, Missed, Upcoming, Rest.
public record WidgetDayDto(int DayOfWeek, string State);

public record WidgetTodayDto(
    DateOnly TrainingDay,
    List<WidgetSlotDto> Today,
    WidgetNextDto? Next,
    int WeekDone,
    int WeekPlanned,
    List<WidgetDayDto> Days);
