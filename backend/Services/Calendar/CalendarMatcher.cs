using System.Text.RegularExpressions;
using Callahan.Api.Models;

namespace Callahan.Api.Services.Calendar;

// Pairs the week's calendar events with the program's session slots. One event
// fills at most one slot and one slot takes at most one event.
public static partial class CalendarMatcher
{
    public record SlotKey(int SlotId, PlanSlotKind Kind, string Label, string? LinkedUid);

    public record Result(Dictionary<int, CalendarEvent> BySlot, List<CalendarEvent> Other);

    // The titles that mean this slot. Gym and Field slots answer to the short name the
    // program gives them ("Gym 1", "Field 2" - the seeded Field labels carry a subtitle,
    // "Field 1 - Acceleration & Jumps", which nobody types into a calendar); the Aerobic slot is seeded as "Aerobic
    // - easy ride, or rest" but is written in the calendar as "aerobic run", so it
    // answers to "aerobic" and "run". Routines and rest never get an event.
    public static IReadOnlyList<string> MatchKeys(PlanSlotKind kind, string label) => kind switch
    {
        PlanSlotKind.Gym or PlanSlotKind.Field => [Normalise(WeekSoFarBuilder.ShortLabel(label))],
        PlanSlotKind.Aerobic => ["aerobic", "run"],
        _ => []
    };

    // Order of precedence: a link already made (the slot's own event UID), then the
    // title. Events are taken earliest first, slots in the order given.
    public static Result Match(IReadOnlyCollection<SlotKey> slots, IReadOnlyCollection<CalendarEvent> events)
    {
        var bySlot = new Dictionary<int, CalendarEvent>();
        var taken = new HashSet<string>();
        var ordered = events.OrderBy(e => e.Day).ThenBy(e => e.Part ?? TimeOfDay.Morning).ThenBy(e => e.Uid, StringComparer.Ordinal).ToList();

        foreach (var slot in slots)
        {
            if (slot.LinkedUid is null) continue;
            var linked = ordered.FirstOrDefault(e => e.Uid == slot.LinkedUid && !taken.Contains(e.Uid));
            if (linked is null) continue;
            bySlot[slot.SlotId] = linked;
            taken.Add(linked.Uid);
        }

        foreach (var ev in ordered)
        {
            if (taken.Contains(ev.Uid)) continue;
            var title = Normalise(ev.Title);
            var slot = slots.FirstOrDefault(s =>
                !bySlot.ContainsKey(s.SlotId) && MatchKeys(s.Kind, s.Label).Any(k => StartsWithWord(title, k)));
            if (slot is null) continue;
            bySlot[slot.SlotId] = ev;
            taken.Add(ev.Uid);
        }

        return new Result(bySlot, ordered.Where(e => !taken.Contains(e.Uid)).ToList());
    }

    private static string Normalise(string s) => Whitespace().Replace(s.Trim().ToLowerInvariant(), " ");

    // "gym 1" matches "gym 1" and "gym 1 - upper" but not "gym 10".
    private static bool StartsWithWord(string title, string key) =>
        title.StartsWith(key, StringComparison.Ordinal)
        && (title.Length == key.Length || !char.IsLetterOrDigit(title[key.Length]));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
