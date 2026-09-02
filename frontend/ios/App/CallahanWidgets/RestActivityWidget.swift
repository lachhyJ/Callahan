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
                Image(systemName: context.isStale ? "checkmark" : "timer")
                    .foregroundStyle(Self.accent)
            } compactTrailing: {
                if !context.isStale {
                    Countdown(context: context, font: .caption2.monospacedDigit())
                        .frame(minWidth: 40, alignment: .trailing)
                }
            } minimal: {
                Image(systemName: context.isStale ? "checkmark" : "timer")
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
        Text(timerInterval: context.state.startAt...context.state.endAt, countsDown: true)
            .font(font)
            .monospacedDigit()
            .lineLimit(1)
            .foregroundStyle(RestActivityWidget.accent)
    }
}

/// Counts up from the start of the workout, the way Hevy shows session elapsed.
@available(iOS 16.2, *)
private struct ElapsedLabel: View {
    let since: Date

    var body: some View {
        // The range span decides how much width the view reserves: an 8-hour
        // range lays out for "h:mm:ss" and then renders "1:--" in the space a
        // header line can spare. An hour is plenty for a gym session's elapsed
        // readout and keeps it to "m:ss".
        Text(timerInterval: since...since.addingTimeInterval(60 * 60), countsDown: false)
            .font(.caption)
            .monospacedDigit()
            .foregroundStyle(.secondary)
            .lineLimit(1)
            .layoutPriority(1)
            .frame(minWidth: 68, alignment: .trailing)
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
                Text(context.attributes.exerciseName)
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                Text(context.attributes.nextSetLine)
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
        ProgressView(timerInterval: context.state.startAt...context.state.endAt,
                     countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
            .tint(RestActivityWidget.accent)
    }
}

/// -15s · countdown · +15s · Skip, matching Hevy's control row.
@available(iOS 16.2, *)
private struct ControlRow: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        HStack(spacing: 8) {
            if #available(iOS 17.0, *) {
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
                          font: .system(size: 22, weight: .semibold, design: .rounded))
                    .layoutPriority(1)
                    .frame(minWidth: 100)
            }
            Spacer(minLength: 0)

            if #available(iOS 17.0, *) {
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
