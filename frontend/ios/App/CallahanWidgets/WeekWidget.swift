import SwiftUI
import WidgetKit

/// How much of the program's week is done, from `GET /api/widget/week`.
///
/// The server decides what counts: a session fills one of the program's slots when
/// it was logged on any day of the Monday-Sunday week (not only on the planner's
/// day for it), and Field takes Field 1, Field 2, Pod and Solo. The widget just draws
/// it. It also sends the training day, so the widget never works out "today" from the
/// device clock except to notice that what it holds is out of date.
///
/// Meaning is carried by shape (a filled or an empty dot, a filled or an outlined
/// chip), because Clear and Tinted recolour everything to one accent.

// MARK: - Data

struct WeekPayload: Codable, Equatable {
    struct Tally: Codable, Equatable {
        let done: Int
        let total: Int
    }

    struct Chip: Codable, Equatable {
        /// G1, F2, Pod, Solo, Run.
        let short: String
        /// Whether it filled one of the program's sessions for the week.
        let counted: Bool
    }

    struct Day: Codable, Equatable {
        /// 0 = Monday ... 6 = Sunday.
        let dayOfWeek: Int
        let chips: [Chip]
    }

    /// "yyyy-MM-dd".
    let weekStart: String
    let trainingDay: String
    let gym: Tally
    let field: Tally
    /// Program sessions still to do, in program order ("Field 2").
    let left: [String]
    let days: [Day]

    var done: Int { gym.done + field.done }
    var total: Int { gym.total + field.total }

    /// Names while there are one or two, a count when there are more, and "Week
    /// done" once nothing is left.
    var leftText: String {
        switch left.count {
        case 0: return "Week done"
        case 1, 2: return "Left: " + left.joined(separator: ", ")
        default: return "\(left.count) to do"
        }
    }
}

enum WeekState {
    case ok(WeekPayload, fetchFailed: Bool)
    case notConfigured
    case error(String)
}

struct WeekEntry: TimelineEntry {
    let date: Date
    let state: WeekState
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
/// training day than the one it was fetched for (the overnight rollover, and above
/// all the Monday reset, before the next refresh lands).
private func isOutdated(_ payload: WeekPayload, now: Date) -> Bool {
    (dayDifference(from: payload.trainingDay, to: deviceTrainingDay(now: now)) ?? 0) >= 1
}

/// Monday = 0, matching `WeekPayload.Day.dayOfWeek`.
private func weekdayIndex(of isoDate: String) -> Int? {
    guard let date = isoParser.date(from: isoDate) else { return nil }
    let weekday = Calendar(identifier: .gregorian).component(.weekday, from: date) // 1 = Sunday
    return (weekday + 5) % 7
}

// MARK: - Provider

struct WeekProvider: TimelineProvider {

    private static let cacheKey = "callahan.widget.lastWeek"
    /// What the week looks like changes when something is logged (the app asks for a
    /// reload then) or when the week turns over, so hourly is a backstop; a failure
    /// retries sooner.
    private static let refreshInterval: TimeInterval = 60 * 60
    private static let retryInterval: TimeInterval = 15 * 60

    func placeholder(in context: Context) -> WeekEntry {
        WeekEntry(date: .now, state: .ok(WeekPayload.sample, fetchFailed: false))
    }

    func getSnapshot(in context: Context, completion: @escaping (WeekEntry) -> Void) {
        completion(placeholder(in: context))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<WeekEntry>) -> Void) {
        Task {
            let now = Date()
            let (state, next) = await Self.load()
            let entry = WeekEntry(date: now, state: state)
            completion(Timeline(entries: [entry], policy: .after(now.addingTimeInterval(next))))
        }
    }

    /// The state to show and how long until the next fetch.
    private static func load() async -> (WeekState, TimeInterval) {
        switch await WidgetAPI.fetch("/api/widget/week", as: WeekPayload.self) {
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

    /// Keep showing the last good week (marked as not refreshed) rather than blanking
    /// the widget over a dropped connection.
    private static func failure(_ message: String) -> (WeekState, TimeInterval) {
        if let cached = WidgetAPI.stored(key: cacheKey, as: WeekPayload.self) {
            return (.ok(cached, fetchFailed: true), retryInterval)
        }
        return (.error(message), retryInterval)
    }
}

extension WeekPayload {
    /// A Wednesday with two sessions in, for the gallery and placeholder.
    static let sample = WeekPayload(
        weekStart: "2026-09-28",
        trainingDay: "2026-09-30",
        gym: .init(done: 1, total: 3),
        field: .init(done: 1, total: 2),
        left: ["Gym 2", "Field 2", "Gym 3"],
        days: [
            .init(dayOfWeek: 0, chips: [.init(short: "G1", counted: true)]),
            .init(dayOfWeek: 1, chips: [.init(short: "F1", counted: true)]),
            .init(dayOfWeek: 2, chips: []),
            .init(dayOfWeek: 3, chips: []),
            .init(dayOfWeek: 4, chips: []),
            .init(dayOfWeek: 5, chips: []),
            .init(dayOfWeek: 6, chips: []),
        ])
}

// MARK: - Views

struct WeekWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: WeekEntry

    var body: some View {
        Group {
            switch entry.state {
            case let .ok(payload, fetchFailed):
                WeekContent(payload: payload, fetchFailed: fetchFailed, now: entry.date, family: family)
            case .notConfigured:
                WeekMessage(title: "Not configured", detail: "Add the widget key and rebuild")
            case let .error(message):
                WeekMessage(title: "This week", detail: message)
            }
        }
        .widgetURL(DeepLink.workouts)
        .containerBackground(.fill.tertiary, for: .widget)
    }
}

private struct WeekContent: View {
    let payload: WeekPayload
    let fetchFailed: Bool
    let now: Date
    let family: WidgetFamily

    private var outdated: Bool { isOutdated(payload, now: now) }
    private var todayIndex: Int? { weekdayIndex(of: payload.trainingDay) }

    var body: some View {
        Group {
            if family == .systemSmall { small } else { medium }
        }
        .opacity(outdated ? 0.55 : 1)
    }

    private var small: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("THIS WEEK")
                .font(.caption2.weight(.semibold))
                .foregroundStyle(.secondary)
            tallyRow("Gym", payload.gym)
            tallyRow("Field", payload.field)
            Spacer(minLength: 0)
            HStack(spacing: 4) {
                if payload.left.isEmpty { Image(systemName: "checkmark.circle") }
                Text(payload.leftText)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                if outdated || fetchFailed {
                    Image(systemName: "clock.badge.exclamationmark")
                }
            }
            .font(.footnote.weight(.medium))
            .foregroundStyle(payload.left.isEmpty ? .primary : .secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var medium: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("THIS WEEK")
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
                Spacer()
                if outdated || fetchFailed {
                    Image(systemName: "clock.badge.exclamationmark").font(.caption2)
                }
                Text("\(payload.done) of \(payload.total)")
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
            }
            WeekStrip(days: payload.days, todayIndex: todayIndex)
            Spacer(minLength: 0)
            HStack(spacing: 10) {
                tallyInline("Gym", payload.gym)
                tallyInline("Field", payload.field)
                Text("·").foregroundStyle(.secondary)
                Text(payload.leftText)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            }
            .font(.footnote)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func tallyRow(_ label: String, _ tally: WeekPayload.Tally) -> some View {
        HStack {
            Text(label)
            Spacer(minLength: 4)
            Dots(tally: tally, size: 11)
        }
        .font(.callout)
    }

    private func tallyInline(_ label: String, _ tally: WeekPayload.Tally) -> some View {
        HStack(spacing: 5) {
            Text(label)
            Dots(tally: tally, size: 9)
        }
    }
}

/// One dot per session the program asks for, filled once it is done.
private struct Dots: View {
    let tally: WeekPayload.Tally
    let size: CGFloat

    var body: some View {
        HStack(spacing: 4) {
            ForEach(0 ..< tally.total, id: \.self) { i in
                if i < tally.done {
                    Circle().fill(Color.primary).frame(width: size, height: size)
                } else {
                    Circle().strokeBorder(Color.primary, lineWidth: 1.5).frame(width: size, height: size)
                }
            }
        }
    }
}

/// Seven days, Monday first, each with what was logged on it. A chip that filled one
/// of the program's sessions has a soft fill; one that only shows (a third field session, a
/// run, a custom workout) is an outline. Today gets a faint highlight.
private struct WeekStrip: View {
    let days: [WeekPayload.Day]
    let todayIndex: Int?

    private static let initials = ["M", "T", "W", "T", "F", "S", "S"]

    var body: some View {
        HStack(alignment: .top, spacing: 3) {
            ForEach(days, id: \.dayOfWeek) { day in
                VStack(spacing: 3) {
                    Text(Self.initials.indices.contains(day.dayOfWeek) ? Self.initials[day.dayOfWeek] : "")
                        .font(.system(size: 10, weight: .medium))
                        .foregroundStyle(.secondary)
                    ForEach(Array(day.chips.prefix(2).enumerated()), id: \.offset) { _, chip in
                        ChipView(chip: chip)
                    }
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 3)
                .background {
                    if day.dayOfWeek == todayIndex {
                        RoundedRectangle(cornerRadius: 9).fill(Color.primary.opacity(0.14))
                    }
                }
            }
        }
    }
}

private struct ChipView: View {
    let chip: WeekPayload.Chip

    var body: some View {
        Text(chip.short)
            .font(.system(size: 10.5, weight: .medium))
            .lineLimit(1)
            .minimumScaleFactor(0.7)
            .padding(.horizontal, 4)
            .padding(.vertical, 1.5)
            // Text is never inverted: under Clear the pill and its text would both go
            // white. A counted chip is a soft fill with normal text, an uncounted one
            // an outline with dimmer text, so the difference is fill against outline.
            .foregroundStyle(chip.counted ? Color.primary : Color.secondary)
            .background {
                if chip.counted {
                    RoundedRectangle(cornerRadius: 6).fill(Color.primary.opacity(0.26))
                } else {
                    RoundedRectangle(cornerRadius: 6).strokeBorder(Color.secondary.opacity(0.7), lineWidth: 0.75)
                }
            }
    }
}

private struct WeekMessage: View {
    let title: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("THIS WEEK")
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

struct WeekWidget: Widget {
    let kind = "WeekWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: WeekProvider()) { entry in
            WeekWidgetView(entry: entry)
        }
        .configurationDisplayName("This week")
        .description("How much of the week's training is done, and what's left.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

#Preview("Small", as: .systemSmall) {
    WeekWidget()
} timeline: {
    WeekEntry(date: .now, state: .ok(WeekPayload.sample, fetchFailed: false))
    WeekEntry(date: .now, state: .notConfigured)
}

#Preview("Medium", as: .systemMedium) {
    WeekWidget()
} timeline: {
    WeekEntry(date: .now, state: .ok(WeekPayload.sample, fetchFailed: false))
    WeekEntry(date: .now, state: .error("Key rejected"))
}
