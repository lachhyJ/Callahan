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
                    SessionLabel(context: context, font: .caption)
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
                // When another app also has a Live Activity up — music, usually —
                // iOS demotes one of them to this. There is no API to claim the
                // compact slot or the whole island, so the only lever is making
                // the demoted presentation still worth reading: the time left,
                // not a glyph that says "there is a timer somewhere".
                if context.state.isResting && !context.isStale {
                    Countdown(context: context, font: .system(size: 12, weight: .semibold).monospacedDigit())
                } else {
                    Image(systemName: (context.isStale && context.state.isResting) ? "checkmark" : "timer")
                        .foregroundStyle(Self.accent)
                }
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
            .font(.subheadline)
            .monospacedDigit()
            .foregroundStyle(.secondary)
            .lineLimit(1)
            // Safe to fixedSize here in a way a Text(timerInterval:) is not (see
            // the sizing rules on the Widget): this is a plain string computed at
            // update time, so it has one true width and asking for it cannot make
            // the system reserve room for digits it might show later. Without it
            // the session name next to this wins the width negotiation and the
            // elapsed readout gets its last character clipped by the card edge —
            // "0 sec" rendering as "0 se".
            .fixedSize(horizontal: true, vertical: false)
            .layoutPriority(1)
    }

    private var text: String {
        let seconds = max(0, Int(Date().timeIntervalSince(since)))
        if seconds < 60 { return "\(seconds) sec" }
        let minutes = seconds / 60
        if minutes < 60 { return "\(minutes) min" }
        return "\(minutes / 60)h \(minutes % 60)m"
    }
}

/// The header label: which session this is, not that it is a session.
///
/// Truncates rather than pushing the elapsed readout off the card — the elapsed
/// time is a fixed short string and the session name is the elastic one, so the
/// name is what gives way when a long template subtitle meets a narrow island.
@available(iOS 16.2, *)
private struct SessionLabel: View {
    let context: ActivityViewContext<RestActivityAttributes>
    var font: Font

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: "figure.strengthtraining.traditional")
                .font(font)
            Text(context.attributes.sessionLabel)
                .font(font)
                .lineLimit(1)
                .truncationMode(.tail)
        }
        .foregroundStyle(.secondary)
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
                    .font(.system(size: 18))
                    .foregroundStyle(RestActivityWidget.accent)
            }
            .frame(width: 40, height: 40)

            VStack(alignment: .leading, spacing: 2) {
                Text(context.state.exerciseName)
                    .font(.headline.weight(.semibold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
                Text(context.state.nextSetLine)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
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

/// Two shapes, because the card is useful for two different moments.
///
/// While the rest runs: -15s · countdown · +15s · Skip, matching Hevy's control
/// row. Once it is over the adjust buttons have nothing to adjust, so the row
/// becomes the thing you actually want at that point — tick the set you just did
/// and start the next rest, without unlocking the phone.
@available(iOS 16.2, *)
private struct ControlRow: View {
    let context: ActivityViewContext<RestActivityAttributes>

    /// The rest has run out, or there was never one running — either way the next
    /// thing to happen is a set, not an adjustment.
    private var restOver: Bool { !context.state.isResting || context.isStale }
    private var hasSetsLeft: Bool { context.state.nextSetNumber <= context.state.totalSets }

    var body: some View {
        HStack(spacing: 8) {
            if #available(iOS 17.0, *), !restOver {
                Button(intent: AdjustRestIntent(deltaSeconds: -15)) {
                    Text("-15s").font(.subheadline.weight(.medium))
                }
                .buttonStyle(.bordered)
                .tint(.gray)
            }

            Spacer(minLength: 0)
            if context.isStale {
                // The rest is over, so a countdown has nothing left to say. What
                // you want at arm's length is what the next set is loaded to —
                // which used to be buried in the small grey line while this slot
                // said "Go", a label that looked like a button and did nothing.
                LoadedSet(context: context)
            } else {
                Countdown(context: context,
                          font: .system(size: 26, weight: .semibold, design: .rounded))
                    .layoutPriority(1)
                    .frame(width: restOver ? 90 : 150, alignment: .center)
            }
            Spacer(minLength: 0)

            if #available(iOS 17.0, *) {
                if restOver {
                    if hasSetsLeft {
                        // Tick only, no caption. There is exactly one action on
                        // this card and it sits under a line that already says
                        // which set is next — "Set done" spent width restating
                        // that, and width is what the loaded-set readout needs.
                        Button(intent: CompleteSetIntent()) {
                            Image(systemName: "checkmark")
                                .font(.subheadline.weight(.bold))
                                .frame(minWidth: 24)
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(RestActivityWidget.accent)
                        .accessibilityLabel("Set done")
                    }
                } else {
                    Button(intent: AdjustRestIntent(deltaSeconds: 15)) {
                        Text("+15s").font(.subheadline.weight(.medium))
                    }
                    .buttonStyle(.bordered)
                    .tint(.gray)

                    Button(intent: SkipRestIntent()) {
                        Text("Skip").font(.subheadline.weight(.semibold))
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(RestActivityWidget.accent)
                }
            }
        }
    }
}

/// What the next set is loaded to — "35 kg × 6" — in the slot the countdown
/// occupies while a rest is running.
///
/// Scales down rather than truncating: an assisted-chin line like "-20 kg × 8"
/// is longer than a bare countdown ever is, and half a number is worse than a
/// small one. Falls back to a zeroed clock when there is nothing loaded, which
/// is what the card showed between sets before any of this existed.
@available(iOS 16.2, *)
private struct LoadedSet: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        Group {
            if context.state.loadedSetLine.isEmpty {
                Text("0:00")
            } else {
                Text(context.state.loadedSetLine)
            }
        }
        .font(.system(size: 26, weight: .semibold, design: .rounded))
        .monospacedDigit()
        .lineLimit(1)
        .minimumScaleFactor(0.6)
        .foregroundStyle(RestActivityWidget.accent)
        .layoutPriority(1)
    }
}

@available(iOS 16.2, *)
private struct LockScreenView: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        VStack(spacing: 10) {
            HStack(spacing: 8) {
                SessionLabel(context: context, font: .subheadline)
                Spacer(minLength: 4)
                ElapsedLabel(since: context.attributes.sessionStartedAt)
            }
            ExerciseRow(context: context)
            ProgressBar(context: context)
            ControlRow(context: context)
        }
        // 16pt was on top of the padding the system already applies to the Lock
        // Screen presentation, which pushed the header row wider than the card
        // and clipped the elapsed readout against its right edge.
        .padding(.horizontal, 12)
        .padding(.vertical, 14)
    }
}
