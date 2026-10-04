import SwiftUI
import WidgetKit

/// What is planned today and how the week is going, from `GET /api/widget/today`.
///
/// The server decides what "today" is (3 am cutoff, the user-switchable time zone)
/// and sends it back as `trainingDay`; the widget never works it out from the
/// device clock, except to notice that what it holds is out of date. State is carried
/// by glyph *shape* (check, circle, cross, moon) because Clear and Tinted recolour
/// everything to one accent: colour is decoration only, so nothing is lost there.

// MARK: - Data

struct TodayPayload: Codable, Equatable {
    struct Slot: Codable, Equatable {
        let label: String
        let kind: String
        /// Done, Upcoming, Missed, Skipped or Rest.
        let state: String
    }

    struct Next: Codable, Equatable {
        let dayName: String
        let date: String
        let label: String
        let kind: String
    }

    struct Day: Codable, Equatable {
        /// 0 = Monday ... 6 = Sunday.
        let dayOfWeek: Int
        /// Done, Missed, Upcoming or Rest.
        let state: String
    }

    /// "yyyy-MM-dd".
    let trainingDay: String
    let today: [Slot]
    let next: Next?
    let weekDone: Int
    let weekPlanned: Int
    let days: [Day]
}

enum TodayState {
    case ok(TodayPayload, fetchFailed: Bool)
    case notConfigured
    case error(String)
}

struct TodayEntry: TimelineEntry {
    let date: Date
    let state: TodayState
}

// MARK: - Dates

private let isoParser: DateFormatter = {
    let f = DateFormatter()
    f.calendar = Calendar(identifier: .gregorian)
    f.locale = Locale(identifier: "en_US_POSIX")
    f.dateFormat = "yyyy-MM-dd"
    return f
}()

private func dayDifference(from earlier: String, to later: String) -> Int? {
    guard let a = isoParser.date(from: earlier), let b = isoParser.date(from: later) else { return nil }
    let cal = Calendar(identifier: .gregorian)
    return cal.dateComponents([.day], from: cal.startOfDay(for: a), to: cal.startOfDay(for: b)).day
}

/// The device's own training day for `now` (a day runs until 3 am), as "yyyy-MM-dd".
private func deviceTrainingDay(now: Date) -> String {
    isoParser.string(from: now.addingTimeInterval(-3 * 3600))
}

/// What the widget holds is out of date once the device has moved on to a later
/// training day than the one it was fetched for (the overnight rollover, before the
/// next refresh lands).
private func isOutdated(_ payload: TodayPayload, now: Date) -> Bool {
    (dayDifference(from: payload.trainingDay, to: deviceTrainingDay(now: now)) ?? 0) >= 1
}

/// Monday = 0, matching `TodayPayload.Day.dayOfWeek`.
private func weekdayIndex(of isoDate: String) -> Int? {
    guard let date = isoParser.date(from: isoDate) else { return nil }
    let weekday = Calendar(identifier: .gregorian).component(.weekday, from: date) // 1 = Sunday
    return (weekday + 5) % 7
}

/// Program slot labels carry a description after a dash ("Field 1 - Acceleration &
/// Jumps", "Aerobic - easy ride, or rest"); a widget has room for the part before it.
private func shortLabel(_ label: String) -> String {
    label.components(separatedBy: " - ").first ?? label
}

// MARK: - Glyphs

private enum Glyph {
    static func symbol(forSlot state: String) -> String {
        switch state {
        case "Done": return "checkmark.circle.fill"
        case "Missed": return "xmark.circle.fill"
        case "Skipped": return "minus.circle"
        case "Rest": return "moon.zzz.fill"
        default: return "circle" // Upcoming
        }
    }

    /// Decoration only: under Clear and Tinted the system overrides it.
    static func color(forState state: String) -> Color {
        switch state {
        case "Done": return .green
        case "Missed": return .red
        case "Upcoming": return .primary
        default: return .secondary
        }
    }
}

// MARK: - Provider

struct TodayProvider: TimelineProvider {

    private static let cacheKey = "callahan.widget.lastToday"
    /// What is planned changes when something is logged (the app asks for a reload
    /// then) or when the day turns over, so hourly is a backstop; a failure retries sooner.
    private static let refreshInterval: TimeInterval = 60 * 60
    private static let retryInterval: TimeInterval = 15 * 60

    func placeholder(in context: Context) -> TodayEntry {
        TodayEntry(date: .now, state: .ok(TodayPayload.sample, fetchFailed: false))
    }

    func getSnapshot(in context: Context, completion: @escaping (TodayEntry) -> Void) {
        completion(placeholder(in: context))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<TodayEntry>) -> Void) {
        Task {
            let now = Date()
            let (state, next) = await Self.load()
            let entry = TodayEntry(date: now, state: state)
            completion(Timeline(entries: [entry], policy: .after(now.addingTimeInterval(next))))
        }
    }

    /// The state to show and how long until the next fetch.
    private static func load() async -> (TodayState, TimeInterval) {
        switch await WidgetAPI.fetch("/api/widget/today", as: TodayPayload.self) {
        case let .ok(payload):
            WidgetAPI.store(payload, key: cacheKey)
            return (.ok(payload, fetchFailed: false), refreshInterval)
        case .noContent:
            // The route never answers 204; treat it as a transient failure if it does.
            return failure("Nothing to show")
        case .rejected:
            return (.error("Key rejected"), refreshInterval)
        case .notConfigured:
            return (.notConfigured, refreshInterval)
        case let .failure(message):
            return failure(message)
        }
    }

    /// Keep showing the last good plan (marked as not refreshed) rather than blanking
    /// the widget over a dropped connection.
    private static func failure(_ message: String) -> (TodayState, TimeInterval) {
        if let cached = WidgetAPI.stored(key: cacheKey, as: TodayPayload.self) {
            return (.ok(cached, fetchFailed: true), retryInterval)
        }
        return (.error(message), retryInterval)
    }
}

extension TodayPayload {
    static let sample = TodayPayload(
        trainingDay: "2026-10-01",
        today: [
            .init(label: "Gym 2", kind: "Gym", state: "Upcoming"),
            .init(label: "Ankle circuit", kind: "Routine", state: "Done"),
        ],
        next: .init(dayName: "Thursday", date: "2026-10-01", label: "Gym 2", kind: "Gym"),
        weekDone: 2,
        weekPlanned: 5,
        days: [
            .init(dayOfWeek: 0, state: "Done"), .init(dayOfWeek: 1, state: "Rest"),
            .init(dayOfWeek: 2, state: "Done"), .init(dayOfWeek: 3, state: "Upcoming"),
            .init(dayOfWeek: 4, state: "Upcoming"), .init(dayOfWeek: 5, state: "Upcoming"),
            .init(dayOfWeek: 6, state: "Rest"),
        ])
}

// MARK: - Views

struct TodayWidgetView: View {
    @Environment(\.widgetFamily) private var family
    @Environment(\.widgetRenderingMode) private var renderingMode
    let entry: TodayEntry

    var body: some View {
        Group {
            switch entry.state {
            case let .ok(payload, fetchFailed):
                TodayContent(
                    payload: payload,
                    fetchFailed: fetchFailed,
                    now: entry.date,
                    family: family,
                    colorful: renderingMode == .fullColor)
            case .notConfigured:
                TodayMessage(title: "Not configured", detail: "Add the widget key and rebuild")
            case let .error(message):
                TodayMessage(title: "Today", detail: message)
            }
        }
        .widgetURL(DeepLink.workouts)
        .containerBackground(.fill.tertiary, for: .widget)
    }
}

private struct TodayContent: View {
    let payload: TodayPayload
    let fetchFailed: Bool
    let now: Date
    let family: WidgetFamily
    let colorful: Bool

    private var outdated: Bool { isOutdated(payload, now: now) }
    private var todayIndex: Int? { weekdayIndex(of: payload.trainingDay) }
    private var allTodayDone: Bool {
        !payload.today.isEmpty && payload.today.allSatisfy { $0.state == "Done" || $0.state == "Skipped" }
    }

    /// What to point at under today's rows: only worth saying once today has nothing
    /// left (or nothing at all), otherwise today's own rows are the answer.
    private var nextText: String? {
        guard let next = payload.next, payload.today.isEmpty || allTodayDone else { return nil }
        switch dayDifference(from: payload.trainingDay, to: next.date) {
        case 0: return "Next: \(shortLabel(next.label))"
        case 1: return "Tomorrow · \(shortLabel(next.label))"
        default: return "\(next.dayName.prefix(3)) · \(shortLabel(next.label))"
        }
    }

    var body: some View {
        Group {
            if family == .systemSmall { small } else { medium }
        }
        .opacity(outdated ? 0.55 : 1)
    }

    private var small: some View {
        VStack(alignment: .leading, spacing: 5) {
            Text("TODAY")
                .font(.caption2.weight(.semibold))
                .foregroundStyle(.secondary)
            rows(limit: 3, font: .footnote)
            if let nextText {
                Text(nextText)
                    .font(.footnote.weight(.medium))
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            }
            Spacer(minLength: 0)
            WeekStrip(days: payload.days, todayIndex: todayIndex, colorful: colorful, large: false)
            footer
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var medium: some View {
        HStack(alignment: .top, spacing: 16) {
            VStack(alignment: .leading, spacing: 6) {
                Text("TODAY")
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
                rows(limit: 4, font: .callout)
                if let nextText {
                    Text(nextText)
                        .font(.callout.weight(.medium))
                        .lineLimit(1)
                        .minimumScaleFactor(0.8)
                }
                Spacer(minLength: 0)
                staleNote
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            VStack(alignment: .leading, spacing: 6) {
                Text("THIS WEEK")
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
                progress(font: .title)
                WeekStrip(days: payload.days, todayIndex: todayIndex, colorful: colorful, large: true)
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    @ViewBuilder
    private func rows(limit: Int, font: Font) -> some View {
        if payload.today.isEmpty {
            Text(payload.next == nil ? "Nothing planned" : "Nothing today")
                .font(font)
                .foregroundStyle(.secondary)
        } else {
            VStack(alignment: .leading, spacing: 3) {
                ForEach(Array(payload.today.prefix(limit).enumerated()), id: \.offset) { _, slot in
                    HStack(spacing: 6) {
                        Image(systemName: Glyph.symbol(forSlot: slot.state))
                            .foregroundStyle(colorful ? Glyph.color(forState: slot.state) : Color.primary)
                        Text(shortLabel(slot.label))
                            .lineLimit(1)
                            .foregroundStyle(slot.state == "Done" ? .secondary : .primary)
                    }
                    .font(font)
                }
            }
        }
    }

    private func progress(font: Font) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 3) {
            Text("\(payload.weekDone)")
                .font(font.weight(.bold))
                .widgetAccentable()
            Text("of \(payload.weekPlanned)")
                .font(.footnote)
                .foregroundStyle(.secondary)
        }
    }

    /// Small only: progress and the not-up-to-date note share one line.
    private var footer: some View {
        HStack(spacing: 4) {
            Text("\(payload.weekDone) of \(payload.weekPlanned) this week")
                .font(.caption2.weight(.medium))
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            if outdated || fetchFailed {
                Image(systemName: "clock.badge.exclamationmark").font(.caption2)
            }
        }
    }

    @ViewBuilder private var staleNote: some View {
        if outdated || fetchFailed {
            HStack(spacing: 4) {
                Image(systemName: "clock.badge.exclamationmark")
                Text(outdated ? "Not up to date" : "Not refreshed")
            }
            .font(.caption2.weight(.medium))
        }
    }
}

/// Seven glyphs, Monday first, one per day of the week. Today gets a faint capsule.
private struct WeekStrip: View {
    let days: [TodayPayload.Day]
    let todayIndex: Int?
    let colorful: Bool
    /// The medium widget has room for the weekday initials above each glyph.
    let large: Bool

    private static let initials = ["M", "T", "W", "T", "F", "S", "S"]

    var body: some View {
        HStack(spacing: large ? 5 : 4) {
            ForEach(days, id: \.dayOfWeek) { day in
                VStack(spacing: 2) {
                    if large, Self.initials.indices.contains(day.dayOfWeek) {
                        Text(Self.initials[day.dayOfWeek])
                            .font(.system(size: 9, weight: .medium))
                            .foregroundStyle(.secondary)
                    }
                    Image(systemName: Glyph.symbol(forSlot: day.state))
                        .font(.system(size: large ? 13 : 11))
                        .foregroundStyle(colorful ? Glyph.color(forState: day.state) : Color.primary)
                }
                .padding(.horizontal, 2)
                .padding(.vertical, 2)
                .background {
                    if day.dayOfWeek == todayIndex {
                        Capsule().fill(Color.primary.opacity(0.14))
                    }
                }
            }
        }
    }
}

private struct TodayMessage: View {
    let title: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("TODAY")
                .font(.caption2.weight(.semibold))
                .foregroundStyle(.secondary)
            Spacer(minLength: 0)
            Text(title).font(.headline)
            Text(detail).font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

// MARK: - Widget

struct TodayWidget: Widget {
    let kind = "TodayWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: TodayProvider()) { entry in
            TodayWidgetView(entry: entry)
        }
        .configurationDisplayName("Today")
        .description("What's planned today and how the week is going.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

#Preview("Small", as: .systemSmall) {
    TodayWidget()
} timeline: {
    TodayEntry(date: .now, state: .ok(TodayPayload.sample, fetchFailed: false))
    TodayEntry(date: .now, state: .notConfigured)
}

#Preview("Medium", as: .systemMedium) {
    TodayWidget()
} timeline: {
    TodayEntry(date: .now, state: .ok(TodayPayload.sample, fetchFailed: false))
    TodayEntry(date: .now, state: .error("Key rejected"))
}
