using System.Text.RegularExpressions;
using Callahan.Api.DTOs;
using Callahan.Api.Models;

namespace Callahan.Api.Services;

// What the "this week" widget shows: how much of the program's week has been done,
// by what was logged and when, not by where the planner put each slot.
//
// The planner matches a log to a slot only on the slot's own day, so a week that
// shifts (shift work, a moved session) reads as a row of misses. This asks a
// different question - "has the program's Gym 2 been logged anywhere this week?" -
// which is also how the weekly streaks already count. Pure, like WeekPlanBuilder:
// the week and the logs come in, so every rule here is testable without a database.
public static partial class WeekSoFarBuilder
{
    // Session types that still count as a field session although their family is
    // not Field: Solo and Pod are Ultimate-family types (ids from the seed in
    // AppDbContext). Club Training, Game and Throws are shown but do not count.
    public static readonly IReadOnlySet<int> CountsAsField = new HashSet<int> { 4, 6 };

    // The program's sessions for the week, in program order. The loader passes only
    // non-optional Gym and Field slots; routines (Jump Block, the ankle circuit),
    // rest markers and the optional aerobic slot are deliberately not in the tally.
    public record SlotInput(PlanSlotKind Kind, string Label, int? WorkoutTemplateId, int? ActivitySessionTypeId);

    public record GymLog(DateOnly Date, int? TemplateId, string? TemplateName);

    public record ActivityLog(
        DateOnly Date, ActivityType Type, int? SessionTypeId, string? SessionTypeName, SessionTypeFamily? Family);

    public static WidgetWeekDto Build(
        DateOnly weekStart,
        DateOnly trainingDay,
        IReadOnlyList<SlotInput> slots,
        IReadOnlyList<GymLog> gymLogs,
        IReadOnlyList<ActivityLog> activityLogs)
    {
        // Only this Monday-Sunday counts, whatever the caller passed in.
        bool InWeek(DateOnly d) => d >= weekStart && d <= weekStart.AddDays(6);
        gymLogs = gymLogs.Where(g => InWeek(g.Date)).ToList();
        activityLogs = activityLogs.Where(a => InWeek(a.Date)).ToList();

        var gymSlots = slots.Where(s => s.Kind == PlanSlotKind.Gym).ToList();
        var fieldSlots = slots.Where(s => s.Kind == PlanSlotKind.Field).ToList();

        var fill = Fill(gymSlots, fieldSlots, gymLogs, activityLogs);
        var gymUsed = fill.GymUsed;
        var activityUsed = fill.ActivityUsed;
        var gymFilled = fill.GymFilled;
        var fieldFilled = fill.FieldFilled;

        var days = Enumerable.Range(0, 7)
            .Select(d => (Date: weekStart.AddDays(d), Chips: new List<WidgetChipDto>()))
            .ToList();
        void Place(DateOnly date, WidgetChipDto chip)
        {
            var offset = date.DayNumber - weekStart.DayNumber;
            if (offset is >= 0 and < 7) days[offset].Chips.Add(chip);
        }

        // Everything logged is shown, counted or not; gym first within a day.
        for (var j = 0; j < gymLogs.Count; j++)
        {
            Place(gymLogs[j].Date, new WidgetChipDto(GymChip(gymLogs[j].TemplateName), gymUsed[j]));
        }
        for (var j = 0; j < activityLogs.Count; j++)
        {
            Place(activityLogs[j].Date, new WidgetChipDto(ActivityChip(activityLogs[j]), activityUsed[j]));
        }

        // What is left, in the program's own order (slots interleave Gym and Field).
        var left = new List<string>();
        int gi = 0, fi = 0;
        foreach (var slot in slots)
        {
            if (slot.Kind == PlanSlotKind.Gym) { if (!gymFilled[gi++]) left.Add(ShortLabel(slot.Label)); }
            else if (slot.Kind == PlanSlotKind.Field) { if (!fieldFilled[fi++]) left.Add(ShortLabel(slot.Label)); }
        }

        return new WidgetWeekDto(
            weekStart,
            trainingDay,
            new WidgetTallyDto(gymFilled.Count(f => f), gymSlots.Count),
            new WidgetTallyDto(fieldFilled.Count(f => f), fieldSlots.Count),
            left,
            days.Select((d, i) => new WidgetWeekDayDto(i, d.Chips)).ToList());
    }

    // Which of the program's gym and field slots the week's logs fill, and which
    // logs were used up doing it. Shared with the Plan page (WeekPlanBuilder), so the
    // widget and the page cannot disagree about whether a session was done.
    public record FillResult(bool[] GymFilled, bool[] FieldFilled, bool[] GymUsed, bool[] ActivityUsed);

    public static FillResult Fill(
        IReadOnlyList<SlotInput> gymSlots,
        IReadOnlyList<SlotInput> fieldSlots,
        IReadOnlyList<GymLog> gymLogs,
        IReadOnlyList<ActivityLog> activityLogs)
    {
        var gymUsed = new bool[gymLogs.Count];
        var activityUsed = new bool[activityLogs.Count];

        // Gym: a slot is done when its own template was logged any day this week.
        // One log fills one slot, so doing Gym 1 twice leaves a second, uncounted chip.
        var gymFilled = new bool[gymSlots.Count];
        for (var i = 0; i < gymSlots.Count; i++)
        {
            var at = IndexOf(gymLogs.Count, j => !gymUsed[j] && gymSlots[i].WorkoutTemplateId is int t && gymLogs[j].TemplateId == t);
            if (at < 0) continue;
            gymUsed[at] = true;
            gymFilled[i] = true;
        }

        // Field, in two passes. First each slot takes an exact match on its own
        // session type (Field 1, Field 2); then any slot still open takes the
        // earliest unused session that counts as field (Pod, Solo, or another Field
        // type), so a Pod session stands in for a Field 2 that was not logged.
        var fieldFilled = new bool[fieldSlots.Count];
        for (var i = 0; i < fieldSlots.Count; i++)
        {
            var at = IndexOf(activityLogs.Count, j =>
                !activityUsed[j] && fieldSlots[i].ActivitySessionTypeId is int t && activityLogs[j].SessionTypeId == t);
            if (at < 0) continue;
            activityUsed[at] = true;
            fieldFilled[i] = true;
        }
        for (var i = 0; i < fieldSlots.Count; i++)
        {
            if (fieldFilled[i]) continue;
            var at = EarliestUnused(activityLogs, activityUsed, CountsTowardField);
            if (at < 0) break;
            activityUsed[at] = true;
            fieldFilled[i] = true;
        }

        return new FillResult(gymFilled, fieldFilled, gymUsed, activityUsed);
    }

    private static bool CountsTowardField(ActivityLog a) =>
        a.Family == SessionTypeFamily.Field || (a.SessionTypeId is int id && CountsAsField.Contains(id));

    private static int IndexOf(int count, Func<int, bool> match)
    {
        for (var j = 0; j < count; j++) if (match(j)) return j;
        return -1;
    }

    // Earliest by date; the input order breaks ties, so it is stable.
    private static int EarliestUnused(IReadOnlyList<ActivityLog> logs, bool[] used, Func<ActivityLog, bool> counts)
    {
        var best = -1;
        for (var j = 0; j < logs.Count; j++)
        {
            if (used[j] || !counts(logs[j])) continue;
            if (best < 0 || logs[j].Date < logs[best].Date) best = j;
        }
        return best;
    }

    // "Field 2 - Repeat Effort & COD" -> "Field 2"; "Gym 2" stays.
    public static string ShortLabel(string label) => label.Split(" - ")[0];

    // "Gym 1" -> "G1". A custom workout (no template, or one that is not Gym n) is "Gym".
    private static string GymChip(string? templateName)
    {
        var m = templateName is null ? null : GymNumber().Match(templateName);
        return m is { Success: true } ? $"G{m.Groups[1].Value}" : "Gym";
    }

    // "Field 1 - ..." -> "F1"; a running session -> "Run"; otherwise the first word,
    // at most five characters (Pod, Solo, Club, Game, Throw). No session type:
    // "Run" for a run, "Field" for anything else.
    private static string ActivityChip(ActivityLog a)
    {
        if (a.SessionTypeName is null) return a.Type == ActivityType.Running ? "Run" : "Field";
        var f = FieldNumber().Match(a.SessionTypeName);
        if (f.Success) return $"F{f.Groups[1].Value}";
        if (a.Family == SessionTypeFamily.Run) return "Run";
        var word = a.SessionTypeName.Split(' ')[0];
        return word.Length <= 5 ? word : word[..5];
    }

    [GeneratedRegex(@"^Gym (\d+)")]
    private static partial Regex GymNumber();

    [GeneratedRegex(@"^Field (\d+)")]
    private static partial Regex FieldNumber();
}
