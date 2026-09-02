import ActivityKit
import AppIntents
import SwiftUI
import WidgetKit

/// The rest timer on the lock screen and in the Dynamic Island.
///
/// Countdowns are `Text(timerInterval:)` / `ProgressView(timerInterval:)`, which
/// the system animates from the start/end dates without the app running — the
/// whole point of doing this natively, since iOS suspends the webview's JS in the
/// background.
///
/// Sizing rules, all learned by breaking them:
///   • never `.fixedSize()` a `Text(timerInterval:)` — inside the ProgressView's
///     GeometryReader it trips a layout assertion and crashes the widget process,
///     which silently stops the card rendering at all;
///   • never cap one with `maxWidth` — it lays out for the widest digits it may
///     show and renders "1:--" rather than shrinking;
///   • an unprioritised `minWidth` loses the width negotiation to a long exercise
///     name and truncates anyway, so give the timer `layoutPriority`.
@available(iOS 16.2, *)
struct RestActivityWidget: Widget {

    var body: some WidgetConfiguration {
        ActivityConfiguration(for: RestActivityAttributes.self) { context in
            LockScreenView(context: context)
                .activityBackgroundTint(.black.opacity(0.75))
                .activitySystemActionForegroundColor(.white)

        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    HStack(spacing: 6) {
                        Image(systemName: "figure.strengthtraining.traditional")
                            .font(.caption)
                        Text("Workout").font(.caption)
                    }
                    .foregroundStyle(.secondary)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    ElapsedLabel(since: context.attributes.sessionStartedAt)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(spacing: 8) {
                        ExerciseRow(context: context)
                        ProgressBar(context: context)
                        ControlRow(context: context)
                    }
                    .padding(.top, 2)
                }
            } compactLeading: {
                Image(systemName: (context.isStale && context.state.isResting) ? "checkmark" : "timer")
                    .foregroundStyle(Self.accent)
            } compactTrailing: {
                if context.state.isResting && !context.isStale {
                    Countdown(context: context, font: .caption2.monospacedDigit())
                        .frame(width: 46, alignment: .trailing)
                }
            } minimal: {
                Image(systemName: (context.isStale && context.state.isResting) ? "checkmark" : "timer")
                    .foregroundStyle(Self.accent)
            }
            .keylineTint(Self.accent)
        }
    }

    /// Callahan's purple. Deliberately not Hevy's blue.
    static let accent = Color(red: 0.66, green: 0.33, blue: 0.97)
}

// MARK: - Pieces

@available(iOS 16.2, *)
private struct Countdown: View {
    let context: ActivityViewContext<RestActivityAttributes>
    var font: Font

    var body: some View {
        Group {
            if let endAt = context.state.endAt {
                Text(timerInterval: context.state.startAt...endAt, countsDown: true)
                    .monospacedDigit()
            } else {
                // No rest running, but the workout is still open — Hevy shows a
                // zeroed clock here rather than dropping the card.
                Text("0:00")
            }
        }
        .font(font)
        .lineLimit(1)
        .foregroundStyle(RestActivityWidget.accent)
    }
}

/// Session elapsed, the way Hevy shows it.
///
/// Deliberately not a live `Text(timerInterval:)`: past a few minutes iOS elides
/// the seconds on a counting-up timer and it renders as "38:--", which reads as
/// broken however much width it is given. A value computed at update time is
/// accurate whenever the card changes — which is every set — and looks
/// intentional in between. Hevy shows a coarse elapsed for the same reason.
@available(iOS 16.2, *)
private struct ElapsedLabel: View {
    let since: Date

    var body: some View {
        Text(text)
            .font(.caption)
            .monospacedDigit()
            .foregroundStyle(.secondary)
            .lineLimit(1)
    }

    private var text: String {
        let seconds = max(0, Int(Date().timeIntervalSince(since)))
        if seconds < 60 { return "\(seconds) sec" }
        let minutes = seconds / 60
        if minutes < 60 { return "\(minutes) min" }
        return "\(minutes / 60)h \(minutes % 60)m"
    }
}

@available(iOS 16.2, *)
private struct ExerciseRow: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        HStack(spacing: 10) {
            // Hevy shows a per-exercise illustration; we have no artwork, so a
            // glyph in a tinted circle stands in for it.
            ZStack {
                Circle().fill(RestActivityWidget.accent.opacity(0.18))
                Image(systemName: "dumbbell.fill")
                    .font(.system(size: 15))
                    .foregroundStyle(RestActivityWidget.accent)
            }
            .frame(width: 34, height: 34)

            VStack(alignment: .leading, spacing: 1) {
                Text(context.state.exerciseName)
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                Text(context.state.nextSetLine)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            }
            Spacer(minLength: 0)
        }
    }
}

@available(iOS 16.2, *)
private struct ProgressBar: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        Group {
            if let endAt = context.state.endAt {
                ProgressView(timerInterval: context.state.startAt...endAt,
                             countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
            } else {
                ProgressView(value: 0)
            }
        }
        .tint(RestActivityWidget.accent)
    }
}

/// -15s · countdown · +15s · Skip, matching Hevy's control row.
@available(iOS 16.2, *)
private struct ControlRow: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        HStack(spacing: 8) {
            if #available(iOS 17.0, *), context.state.isResting {
                Button(intent: AdjustRestIntent(deltaSeconds: -15)) {
                    Text("-15s").font(.caption.weight(.medium))
                }
                .buttonStyle(.bordered)
                .tint(.gray)
            }

            Spacer(minLength: 0)
            if context.isStale {
                Text("Go")
                    .font(.system(size: 22, weight: .semibold, design: .rounded))
                    .foregroundStyle(RestActivityWidget.accent)
            } else {
                Countdown(context: context,
                          font: .system(size: 21, weight: .semibold, design: .rounded))
                    .layoutPriority(1)
                    .frame(width: 142, alignment: .center)
            }
            Spacer(minLength: 0)

            if #available(iOS 17.0, *), context.state.isResting {
                Button(intent: AdjustRestIntent(deltaSeconds: 15)) {
                    Text("+15s").font(.caption.weight(.medium))
                }
                .buttonStyle(.bordered)
                .tint(.gray)

                Button(intent: SkipRestIntent()) {
                    Text("Skip").font(.caption.weight(.semibold))
                }
                .buttonStyle(.borderedProminent)
                .tint(RestActivityWidget.accent)
            }
        }
    }
}

@available(iOS 16.2, *)
private struct LockScreenView: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        VStack(spacing: 10) {
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "figure.strengthtraining.traditional")
                        .font(.caption)
                    Text("Workout").font(.caption)
                }
                .foregroundStyle(.secondary)
                Spacer()
                ElapsedLabel(since: context.attributes.sessionStartedAt)
            }
            ExerciseRow(context: context)
            ProgressBar(context: context)
            ControlRow(context: context)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }
}
