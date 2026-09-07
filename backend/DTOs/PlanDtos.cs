namespace Callahan.Api.DTOs;

public record PlanSlotDto(
    int SlotId,
    string Label,
    string Kind,
    string State,
    bool IsOptional,
    bool IsMoved,
    bool IsManual);

public record PlanDayDto(int DayOfWeek, DateOnly Date, string DayName, List<PlanSlotDto> Slots);

// The ankle circuit is daily and unmovable, so it is not a slot - it would be
// seven identical rows nobody can rearrange. It rides along as a per-day tick
// strip instead.
public record AnkleCircuitDto(int RoutineId, List<DateOnly> CompletedDates);

public record WeekPlanDto(
    DateOnly WeekStart,
    List<PlanDayDto> Days,
    List<string> Warnings,
    AnkleCircuitDto? AnkleCircuit);

public record UpdatePlanSlotRequest(DateOnly WeekStart, int? DayOfWeek, string? Status);
