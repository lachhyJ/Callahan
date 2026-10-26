import Foundation

/// One line of the lock-screen quotes list, as `GET /api/widget/quotes` sends it.
struct WidgetQuote: Codable, Equatable {
    let id: Int
    let text: String
}

/// Which quote shows on a given day.
///
/// The pick has to be a pure function of (day, list), not a `randomElement()`:
/// WidgetKit rebuilds the timeline every time the app asks for a reload (launch,
/// foreground, saving a quote) and on its own schedule, and a fresh random draw
/// each time would change today's quote under him.
///
/// Each quote is scored by a hash of the day and its id and the highest score
/// wins (rendezvous hashing). That keeps the pick stable when the list is edited:
/// adding a quote changes a given day's pick only if the new quote outscores the
/// current one (1 in n), and deleting one changes it only if it was the pick. A
/// `hash % count` pick would reshuffle every day the moment the count changed.
///
/// No WidgetKit here so the file compiles on its own for a standalone check.
enum QuotePicker {

    /// The hour the app starts treating it as a new day: before it, a moment still
    /// counts for the day before. Mirrors `CalendarDates.TrainingDayCutoffHour`
    /// (backend) and `TRAINING_DAY_CUTOFF_HOUR` (dateUtils.js); there is no shared
    /// source across C#, JS and Swift, so change them together.
    static let cutoffHour = 3

    /// When the training day containing `date` began (the cutoff hour, device zone).
    /// The device zone is the app's zone in practice: the app prompts to switch zone
    /// when the phone changes, and the widget has to work with no network.
    static func trainingDayStart(containing date: Date, calendar: Calendar = .current) -> Date {
        let day = calendar.component(.hour, from: date) < cutoffHour
            ? calendar.date(byAdding: .day, value: -1, to: date) ?? date
            : date
        return calendar.date(bySettingHour: cutoffHour, minute: 0, second: 0, of: day) ?? day
    }

    /// The training day as "yyyy-MM-dd", the thing the pick is keyed on.
    static func dayKey(for date: Date, calendar: Calendar = .current) -> String {
        let c = calendar.dateComponents([.year, .month, .day], from: trainingDayStart(containing: date, calendar: calendar))
        return String(format: "%04d-%02d-%02d", c.year ?? 0, c.month ?? 0, c.day ?? 0)
    }

    static func pick(from quotes: [WidgetQuote], dayKey: String) -> WidgetQuote? {
        quotes.max { a, b in
            let sa = score(dayKey: dayKey, id: a.id), sb = score(dayKey: dayKey, id: b.id)
            // Equal scores are not expected; the lower id wins so it is still stable.
            return sa != sb ? sa < sb : a.id > b.id
        }
    }

    /// FNV-1a over the UTF-8 bytes, then murmur3's 64-bit finalizer. Swift's own
    /// `Hasher` is seeded per process, so it would pick differently on each run.
    /// FNV-1a alone mixes the last bytes poorly (the day digits sit at the end),
    /// hence the finalizer.
    static func score(dayKey: String, id: Int) -> UInt64 {
        var h: UInt64 = 0xcbf2_9ce4_8422_2325
        for byte in "\(dayKey):\(id)".utf8 {
            h ^= UInt64(byte)
            h = h &* 0x0000_0100_0000_01b3
        }
        h ^= h >> 33
        h = h &* 0xff51_afd7_ed55_8ccd
        h ^= h >> 33
        h = h &* 0xc4ce_b9fe_1a85_ec53
        h ^= h >> 33
        return h
    }
}
