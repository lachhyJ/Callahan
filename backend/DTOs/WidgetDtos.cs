namespace Callahan.Api.DTOs;

// What the iOS home screen widget gets: the status code and the load figures
// behind it, nothing else. Labels and colours stay client-side, as on the web.
public record WidgetTrainingStatusDto(
    DateOnly Date,
    int Code,
    double? AcwrRatio,
    int? AcuteLoad,
    int? ChronicLoad);

// The "this week" widget: how much of the program's week has been done, by what was
// logged, not by the planner's day-by-day matching. `TrainingDay` comes from the
// server (3 am cutoff, the user-switchable zone) so the widget never works out
// "today" from the device clock.
public record WidgetTallyDto(int Done, int Total);

// One thing logged on a day. `Short` is the chip text (G1, F2, Pod, Run); `Counted`
// is whether it filled one of the program's sessions for the week.
public record WidgetChipDto(string Short, bool Counted);

public record WidgetWeekDayDto(int DayOfWeek, List<WidgetChipDto> Chips);

// `Left` names the program sessions still to do, in program order ("Field 2").
public record WidgetWeekDto(
    DateOnly WeekStart,
    DateOnly TrainingDay,
    WidgetTallyDto Gym,
    WidgetTallyDto Field,
    List<string> Left,
    List<WidgetWeekDayDto> Days);
