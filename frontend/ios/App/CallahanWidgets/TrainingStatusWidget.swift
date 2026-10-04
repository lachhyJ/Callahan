import SwiftUI
import WidgetKit

/// Garmin Training Status on the home screen.
///
/// The widget is its own process and free provisioning gives it no App Group, so
/// it cannot read anything the app or webview stored — including the login JWT.
/// It fetches `/api/widget/training-status` itself with a narrow key baked in at
/// build time (Widget.local.xcconfig → Info.plist). That key authenticates this
/// one route and nothing else.

// MARK: - Status table

/// Mirrors `TRAINING_STATUS` in frontend/src/trainingStatus.js and the `--ts-*`
/// colours in frontend/src/App.css. There is no shared source across JS and
/// Swift, so change them together.
private struct StatusStyle {
    let label: String
    let color: Color
    /// Light fills (yellow, grey) need dark text to stay readable.
    let darkText: Bool

    static func forCode(_ code: Int) -> StatusStyle {
        switch code {
        case 1: return .init(label: "Detraining", color: Color(hex: 0x7a7a7a), darkText: false)
        case 2: return .init(label: "Unproductive", color: Color(hex: 0xe8680f), darkText: false)
        case 3: return .init(label: "Overreaching", color: Color(hex: 0xe0302f), darkText: false)
        case 4: return .init(label: "Maintaining", color: Color(hex: 0xf5c130), darkText: true)
        case 5: return .init(label: "Recovery", color: Color(hex: 0x1f7ad6), darkText: false)
        case 6: return .init(label: "Peaking", color: Color(hex: 0x8b5cf6), darkText: false)
        case 7: return .init(label: "Productive", color: Color(hex: 0x16a34a), darkText: false)
        case 8: return .init(label: "Strained", color: Color(hex: 0xd12fc4), darkText: false)
        // A code Garmin adds later renders neutral rather than disappearing.
        default: return .init(label: "Other", color: Color(hex: 0x9ca3af), darkText: true)
        }
    }

    /// The status colour as a capsule *image*. The Tinted and Clear home screen
    /// styles recolour every Color and shape in a widget to one accent, but leave
    /// an Image alone when it opts into full colour (that is how the system
    /// Photos widget keeps its pictures), so this is the one way to keep the
    /// status colour on screen under those styles.
    var swatch: Image {
        let size = CGSize(width: 120, height: 12)
        let rendered = UIGraphicsImageRenderer(size: size).image { _ in
            UIColor(color).setFill()
            UIBezierPath(roundedRect: CGRect(origin: .zero, size: size), cornerRadius: size.height / 2).fill()
        }
        return Image(uiImage: rendered.withRenderingMode(.alwaysOriginal))
    }
}

private extension Image {
    /// Opts the image out of the Tinted / Clear recolouring. The modifier is
    /// iOS 18+ while the target is 17, so older systems just get the tinted image.
    @ViewBuilder func keepsFullColor() -> some View {
        if #available(iOS 18.0, *) {
            widgetAccentedRenderingMode(.fullColor)
        } else {
            self
        }
    }
}

private extension Color {
    init(hex: UInt32) {
        self.init(
            red: Double((hex >> 16) & 0xff) / 255,
            green: Double((hex >> 8) & 0xff) / 255,
            blue: Double(hex & 0xff) / 255)
    }
}

// MARK: - Data

struct TrainingStatusPayload: Codable, Equatable {
    /// "yyyy-MM-dd" — the day Garmin computed the status for.
    let date: String
    let code: Int
    let acwrRatio: Double?
    let acuteLoad: Int?
    let chronicLoad: Int?
}

enum TrainingStatusState {
    case ok(TrainingStatusPayload, fetchFailed: Bool)
    case noData
    case notConfigured
    case error(String)
}

struct TrainingStatusEntry: TimelineEntry {
    let date: Date
    let state: TrainingStatusState
}

/// A status this many days old (or more) is flagged rather than hidden: an old
/// status is still better than none, but it must not read as today's. One day
/// is routine — today's row often isn't filled in yet — so two is the first
/// age worth a warning.
private let staleAfterDays = 2

private func ageInDays(of isoDate: String, now: Date) -> Int? {
    let parser = DateFormatter()
    parser.calendar = Calendar(identifier: .gregorian)
    parser.locale = Locale(identifier: "en_US_POSIX")
    parser.dateFormat = "yyyy-MM-dd"
    guard let day = parser.date(from: isoDate) else { return nil }
    let cal = Calendar.current
    return cal.dateComponents([.day], from: cal.startOfDay(for: day), to: cal.startOfDay(for: now)).day
}

private func relativeDay(_ isoDate: String, now: Date) -> String {
    guard let age = ageInDays(of: isoDate, now: now) else { return isoDate }
    switch age {
    case 0: return "Today"
    case 1: return "Yesterday"
    default:
        let parser = DateFormatter()
        parser.calendar = Calendar(identifier: .gregorian)
        parser.locale = Locale(identifier: "en_US_POSIX")
        parser.dateFormat = "yyyy-MM-dd"
        guard let day = parser.date(from: isoDate) else { return isoDate }
        return day.formatted(.dateTime.day().month(.abbreviated))
    }
}

// MARK: - Provider

struct TrainingStatusProvider: TimelineProvider {

    private static let cacheKey = "callahan.widget.lastTrainingStatus"
    /// Garmin's status changes about daily, so hourly is well inside the refresh
    /// budget; a failure retries sooner.
    private static let refreshInterval: TimeInterval = 60 * 60
    private static let retryInterval: TimeInterval = 15 * 60

    func placeholder(in context: Context) -> TrainingStatusEntry {
        TrainingStatusEntry(date: .now, state: .ok(Self.sample, fetchFailed: false))
    }

    func getSnapshot(in context: Context, completion: @escaping (TrainingStatusEntry) -> Void) {
        completion(placeholder(in: context))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<TrainingStatusEntry>) -> Void) {
        Task {
            let now = Date()
            let (state, next) = await Self.load()
            let entry = TrainingStatusEntry(date: now, state: state)
            completion(Timeline(entries: [entry], policy: .after(now.addingTimeInterval(next))))
        }
    }

    private static let sample = TrainingStatusPayload(
        date: "2026-10-03", code: 7, acwrRatio: 1.1, acuteLoad: 310, chronicLoad: 282)

    /// The state to show and how long until the next fetch.
    private static func load() async -> (TrainingStatusState, TimeInterval) {
        switch await WidgetAPI.fetch("/api/widget/training-status", as: TrainingStatusPayload.self) {
        case let .ok(payload):
            WidgetAPI.store(payload, key: cacheKey)
            return (.ok(payload, fetchFailed: false), refreshInterval)
        case .noContent:
            return (.noData, refreshInterval)
        case .rejected:
            // The server answers 404 for a wrong key and for an unset one alike.
            return (.error("Key rejected"), refreshInterval)
        case .notConfigured:
            return (.notConfigured, refreshInterval)
        case let .failure(message):
            return failure(message)
        }
    }

    /// Keep showing the last good status (marked as not refreshed) rather than
    /// blanking the widget over a dropped connection.
    private static func failure(_ message: String) -> (TrainingStatusState, TimeInterval) {
        if let cached = WidgetAPI.stored(key: cacheKey, as: TrainingStatusPayload.self) {
            return (.ok(cached, fetchFailed: true), retryInterval)
        }
        return (.error(message), retryInterval)
    }
}

// MARK: - Views

struct TrainingStatusWidgetView: View {
    @Environment(\.widgetFamily) private var family
    @Environment(\.widgetRenderingMode) private var renderingMode
    let entry: TrainingStatusEntry

    var body: some View {
        switch entry.state {
        case let .ok(payload, fetchFailed):
            StatusContent(payload: payload, fetchFailed: fetchFailed, now: entry.date, family: family)
                .widgetURL(DeepLink.wellness)
                .containerBackground(for: .widget) {
                    // Full colour paints the whole card in the status colour. Under
                    // Clear / Tinted the system supplies the glass or tint itself, so
                    // the card stays transparent and the colour comes back as a
                    // small full-colour image inside (see StatusContent.swatchBar).
                    if renderingMode == .fullColor {
                        StatusStyle.forCode(payload.code).color
                            .opacity(isStale(payload) ? 0.5 : 1)
                    } else {
                        Color.clear
                    }
                }
        case .noData:
            Message(title: "No status yet", detail: "Nothing synced in the last 14 days")
                .containerBackground(.fill.tertiary, for: .widget)
        case .notConfigured:
            Message(title: "Not configured", detail: "Add the widget key and rebuild")
                .containerBackground(.fill.tertiary, for: .widget)
        case let .error(message):
            Message(title: "Training Status", detail: message)
                .containerBackground(.fill.tertiary, for: .widget)
        }
    }

    private func isStale(_ payload: TrainingStatusPayload) -> Bool {
        (ageInDays(of: payload.date, now: entry.date) ?? 0) >= staleAfterDays
    }
}

private struct StatusContent: View {
    @Environment(\.widgetRenderingMode) private var renderingMode
    let payload: TrainingStatusPayload
    let fetchFailed: Bool
    let now: Date
    let family: WidgetFamily

    private var style: StatusStyle { StatusStyle.forCode(payload.code) }
    private var colorful: Bool { renderingMode == .fullColor }
    /// Text colour: matched to the status fill in full colour, otherwise left to
    /// the system so it reads on whatever glass or tint sits behind it.
    private var ink: Color { colorful ? (style.darkText ? .black : .white) : .primary }

    /// The status colour as a bar, shown only when the card itself is not
    /// painted in it. Dimmed when stale, like the full-colour fill.
    @ViewBuilder private var swatchBar: some View {
        if !colorful {
            style.swatch
                .resizable(capInsets: EdgeInsets(top: 0, leading: 6, bottom: 0, trailing: 6))
                .keepsFullColor()
                .frame(height: 6)
                .opacity(stale ? 0.5 : 1)
        }
    }
    private var stale: Bool { (ageInDays(of: payload.date, now: now) ?? 0) >= staleAfterDays }

    var body: some View {
        if family == .systemSmall {
            VStack(alignment: .leading, spacing: 4) {
                caption
                Spacer(minLength: 0)
                // One line, scaled to fit: wrapping splits the long labels
                // mid-word ("Overreach-ing").
                Text(style.label)
                    .font(.title2.weight(.bold))
                    .minimumScaleFactor(0.6)
                    .lineLimit(1)
                    .widgetAccentable()
                swatchBar
                Spacer(minLength: 0)
                footer
            }
            .foregroundStyle(ink)
            .frame(maxWidth: .infinity, alignment: .leading)
        } else {
            HStack(alignment: .top, spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    caption
                    Spacer(minLength: 0)
                    Text(style.label)
                        .font(.largeTitle.weight(.bold))
                        .minimumScaleFactor(0.5)
                        .lineLimit(1)
                        .widgetAccentable()
                    swatchBar
                    Spacer(minLength: 0)
                    footer
                }
                VStack(alignment: .leading, spacing: 10) {
                    if let acwr = payload.acwrRatio {
                        metric("ACWR", String(format: "%.1f", acwr))
                    }
                    if let acute = payload.acuteLoad {
                        metric("Acute load", "\(acute)")
                    }
                    if let chronic = payload.chronicLoad {
                        metric("Chronic load", "\(chronic)")
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .foregroundStyle(ink)
        }
    }

    private var caption: some View {
        Text("TRAINING STATUS")
            .font(.caption2.weight(.semibold))
            .opacity(0.75)
    }

    private var footer: some View {
        // Stale outranks everything else: the point is that an old colour must
        // not be read as today's.
        let text: String
        if stale {
            text = "Stale · \(relativeDay(payload.date, now: now))"
        } else if fetchFailed {
            text = "Not refreshed · \(relativeDay(payload.date, now: now))"
        } else {
            text = relativeDay(payload.date, now: now)
        }
        return HStack(spacing: 4) {
            if stale || fetchFailed {
                Image(systemName: "clock.badge.exclamationmark")
            }
            Text(text)
        }
        .font(.caption.weight(.medium))
    }

    private func metric(_ name: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(name).font(.caption2).opacity(0.75)
            Text(value).font(.title3.weight(.semibold))
        }
    }
}

private struct Message: View {
    let title: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("TRAINING STATUS")
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

struct TrainingStatusWidget: Widget {
    let kind = "TrainingStatusWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: TrainingStatusProvider()) { entry in
            TrainingStatusWidgetView(entry: entry)
        }
        .configurationDisplayName("Training Status")
        .description("Your Garmin Training Status from Callahan.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

#Preview("Small", as: .systemSmall) {
    TrainingStatusWidget()
} timeline: {
    TrainingStatusEntry(date: .now, state: .ok(
        .init(date: "2026-10-03", code: 7, acwrRatio: 1.1, acuteLoad: 310, chronicLoad: 282), fetchFailed: false))
    TrainingStatusEntry(date: .now, state: .ok(
        .init(date: "2026-09-28", code: 4, acwrRatio: 0.9, acuteLoad: 200, chronicLoad: 230), fetchFailed: false))
    TrainingStatusEntry(date: .now, state: .notConfigured)
}

#Preview("Medium", as: .systemMedium) {
    TrainingStatusWidget()
} timeline: {
    TrainingStatusEntry(date: .now, state: .ok(
        .init(date: "2026-10-03", code: 3, acwrRatio: 1.7, acuteLoad: 431, chronicLoad: 245), fetchFailed: false))
    TrainingStatusEntry(date: .now, state: .error("Key rejected"))
}
