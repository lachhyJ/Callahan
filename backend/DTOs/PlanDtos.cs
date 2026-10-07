namespace Callahan.Api.DTOs;

public record PlanSlotDto(
    int SlotId,
    string Label,
    string Kind,
    string State,
    bool IsOptional,
    bool IsMoved,
    bool IsManual,
    // Set only when the week is laid out from the calendar.
    string? TimeOfDay = null,
    string? CalendarUid = null,
    bool IsLinked = false);

public record PlanDayDto(int DayOfWeek, DateOnly Date, string DayName, List<PlanSlotDto> Slots);

// The ankle circuit is daily and unmovable, so it is not a slot - it would be
// seven identical rows nobody can rearrange. It rides along as a per-day tick
// strip instead.
public record AnkleCircuitDto(int RoutineId, List<DateOnly> CompletedDates);

public record WeekPlanDto(
    DateOnly WeekStart,
    List<PlanDayDto> Days,
    List<string> Warnings,
    AnkleCircuitDto? AnkleCircuit,
    // A calendar is configured. When true and CalendarUnavailable is false, Days holds
    // the sessions the calendar placed, Unplaced the ones with no event yet, and
    // OtherEvents the Training-calendar events that matched no session.
    bool CalendarEnabled = false,
    bool CalendarUnavailable = false,
    string? CalendarMessage = null,
    List<PlanSlotDto>? Unplaced = null,
    List<CalendarEventDto>? OtherEvents = null);

public record CalendarEventDto(string Uid, string Title, DateOnly Day, string? TimeOfDay);

public record UpdatePlanSlotRequest(DateOnly WeekStart, int? DayOfWeek, string? Status);

// CalendarUid null unlinks the slot for that week.
public record LinkPlanSlotRequest(DateOnly WeekStart, string? CalendarUid);
