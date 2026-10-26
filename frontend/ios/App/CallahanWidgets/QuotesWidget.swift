import SwiftUI
import WidgetKit

/// A different one of Lachlan's own quotes each day, in the lock screen's inline
/// slot (the line beside the date).
///
/// Not a training feature; it rides in Callahan so there is one app to re-sign. It
/// fetches the whole list from `/api/widget/quotes` behind the same narrow key as
/// the other widgets, keeps the last good list, and builds a timeline of the next
/// two weeks from it, so it keeps rotating with no network and no app running.
/// Which quote lands on which day is `QuotePicker`'s job.

// MARK: - State

enum QuotesState: Equatable {
    case quote(String)
    case empty
    case notConfigured
    case error(String)
}

struct QuotesEntry: TimelineEntry {
    let date: Date
    let state: QuotesState
}

// MARK: - Provider

struct QuotesProvider: TimelineProvider {

    private static let cacheKey = "callahan.widget.lastQuotes"
    /// Entries built ahead, one per training day (starting at 3 am). A refetch happens well inside
    /// this, so it only matters when the widget cannot refresh for a long stretch.
    private static let daysAhead = 14
    /// An edit made in the app also forces a reload (`WidgetBridge.reload()`), so
    /// this is just the backstop.
    private static let refreshInterval: TimeInterval = 6 * 60 * 60
    private static let cachedRetryInterval: TimeInterval = 60 * 60
    private static let errorRetryInterval: TimeInterval = 15 * 60

    func placeholder(in context: Context) -> QuotesEntry {
        QuotesEntry(date: .now, state: .quote("Keep going"))
    }

    func getSnapshot(in context: Context, completion: @escaping (QuotesEntry) -> Void) {
        completion(placeholder(in: context))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<QuotesEntry>) -> Void) {
        Task {
            let now = Date()
            let (state, next) = await Self.load()
            switch state {
            case let .list(quotes):
                completion(Timeline(entries: Self.entries(from: quotes, now: now), policy: .after(now.addingTimeInterval(next))))
            case let .message(message):
                completion(Timeline(entries: [QuotesEntry(date: now, state: message)], policy: .after(now.addingTimeInterval(next))))
            }
        }
    }

    /// The quotes to build days from, or the one-line message to show instead.
    private enum Loaded {
        case list([WidgetQuote])
        case message(QuotesState)
    }

    static func entries(from quotes: [WidgetQuote], now: Date, calendar: Calendar = .current) -> [QuotesEntry] {
        guard !quotes.isEmpty else { return [QuotesEntry(date: now, state: .empty)] }

        // The quote changes when the app starts a new training day (3 am), not at
        // midnight, so each entry begins at a cutoff.
        let today = QuotePicker.trainingDayStart(containing: now, calendar: calendar)
        return (0..<daysAhead).compactMap { offset -> QuotesEntry? in
            guard let start = calendar.date(byAdding: .day, value: offset, to: today),
                  let quote = QuotePicker.pick(from: quotes, dayKey: QuotePicker.dayKey(for: start, calendar: calendar))
            else { return nil }
            // Today's entry is dated now (an entry in the past is fine, but "now" is
            // honest); the rest start at their cutoff.
            return QuotesEntry(date: offset == 0 ? now : start, state: .quote(quote.text))
        }
    }

    private static func load() async -> (Loaded, TimeInterval) {
        switch await WidgetAPI.fetch("/api/widget/quotes", as: [WidgetQuote].self) {
        case let .ok(quotes):
            WidgetAPI.store(quotes, key: cacheKey)
            return (.list(quotes), refreshInterval)
        case .noContent:
            return (.list([]), refreshInterval)
        case .rejected:
            // Wrong or rotated key: worth seeing rather than quietly serving the cache.
            return (.message(.error("Callahan: widget key")), errorRetryInterval)
        case .notConfigured:
            return (.message(.notConfigured), refreshInterval)
        case let .failure(message):
            // A dropped connection keeps the last list going.
            if let cached = WidgetAPI.stored(key: cacheKey, as: [WidgetQuote].self) {
                return (.list(cached), cachedRetryInterval)
            }
            return (.message(.error(message)), errorRetryInterval)
        }
    }
}

// MARK: - View

struct QuotesWidgetView: View {
    let entry: QuotesEntry

    /// The inline slot is one system-styled line with no room for detail, so the
    /// empty and error states are terse.
    private var line: String {
        switch entry.state {
        case let .quote(text): return text
        case .empty: return "Add a quote in Callahan"
        case .notConfigured: return "Callahan: widget not set up"
        case let .error(message): return message
        }
    }

    var body: some View {
        Text(line)
            .widgetURL(DeepLink.quotes)
            .containerBackground(for: .widget) { Color.clear }
    }
}

// MARK: - Widget

struct QuotesWidget: Widget {
    let kind = "QuotesWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: QuotesProvider()) { entry in
            QuotesWidgetView(entry: entry)
        }
        .configurationDisplayName("Quote of the day")
        .description("A different one of your quotes each day, beside the lock screen date.")
        .supportedFamilies([.accessoryInline])
    }
}

#Preview("Inline", as: .accessoryInline) {
    QuotesWidget()
} timeline: {
    QuotesEntry(date: .now, state: .quote("Bring Him with you"))
    QuotesEntry(date: .now, state: .empty)
}
