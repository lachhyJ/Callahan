using System.Text.RegularExpressions;
using Callahan.Api.Models;

namespace Callahan.Api.Services.Calendar;

// Pairs the week's calendar events with the program's session slots. One event
// fills at most one slot and one slot takes at most one event.
public static partial class CalendarMatcher
{
    public record SlotKey(int SlotId, PlanSlotKind Kind, string Label, string? LinkedUid);

    // Linked: the slots whose event was tied by hand rather than found by title, which
    // are the only ones the page offers to unlink.
    public record Result(Dictionary<int, CalendarEvent> BySlot, List<CalendarEvent> Other, HashSet<int> Linked);

    // The titles that mean this slot. Gym and Field slots answer to the short name the
    // program gives them ("Gym 1", "Field 2"; a label with a subtitle, "Field 1 - Defence",
    // answers to the part before " - "); the Aerobic slot is seeded as "Aerobic
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
        var linkedSlots = new HashSet<int>();
        var taken = new HashSet<string>();
        var ordered = events.OrderBy(e => e.Day).ThenBy(e => e.Part ?? TimeOfDay.Morning).ThenBy(e => e.Uid, StringComparer.Ordinal).ToList();

        foreach (var slot in slots)
        {
            if (slot.LinkedUid is null) continue;
            var linked = ordered.FirstOrDefault(e => e.Uid == slot.LinkedUid && !taken.Contains(e.Uid));
            if (linked is null) continue;
            bySlot[slot.SlotId] = linked;
            linkedSlots.Add(slot.SlotId);
            taken.Add(linked.Uid);
        }

        foreach (var ev in ordered)
        {
            if (taken.Contains(ev.Uid)) continue;
            var title = Normalise(ev.Title);
            var slot = slots.FirstOrDefault(s =>
                !bySlot.ContainsKey(s.SlotId) && MatchKeys(s.Kind, s.Label).Any(k => Names(title, k, s.Kind)));
            if (slot is null) continue;
            bySlot[slot.SlotId] = ev;
            taken.Add(ev.Uid);
        }

        return new Result(bySlot, ordered.Where(e => !taken.Contains(e.Uid)).ToList(), linkedSlots);
    }

    private static string Normalise(string s) => Whitespace().Replace(s.Trim().ToLowerInvariant(), " ");

    // A Gym or Field name may sit anywhere in the title, as a whole phrase: a test day
    // that is also a session reads "T0 backup/Field 1/throws". "gym 1" still never
    // matches "gym 10" or "xgym 1". The aerobic words must start the title, since "run"
    // turns up inside other events ("T0 - 2km run test") that aren't the aerobic session.
    private static bool Names(string title, string key, PlanSlotKind kind)
    {
        for (var at = title.IndexOf(key, StringComparison.Ordinal); at >= 0; at = title.IndexOf(key, at + 1, StringComparison.Ordinal))
        {
            if (kind == PlanSlotKind.Aerobic && at > 0) return false;
            var end = at + key.Length;
            if ((at == 0 || !char.IsLetterOrDigit(title[at - 1])) && (end == title.Length || !char.IsLetterOrDigit(title[end])))
            {
                return true;
            }
        }
        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
