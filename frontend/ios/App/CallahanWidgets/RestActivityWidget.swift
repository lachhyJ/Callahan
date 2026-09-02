import ActivityKit
import SwiftUI
import WidgetKit

/// The rest timer on the lock screen and in the Dynamic Island.
///
/// Every countdown here is a `Text(timerInterval:)` / `ProgressView(timerInterval:)`,
/// which the system animates from the start/end dates without the app running.
/// That is the whole point of doing this natively: the web version could not tick
/// while backgrounded because iOS suspends the webview's JS.
@available(iOS 16.2, *)
struct RestActivityWidget: Widget {

    var body: some WidgetConfiguration {
        ActivityConfiguration(for: RestActivityAttributes.self) { context in
            LockScreenView(context: context)
                .activityBackgroundTint(Color.black.opacity(0.55))
                .activitySystemActionForegroundColor(.white)

        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(context.attributes.exerciseName)
                            .font(.headline)
                            .lineLimit(1)
                        Text(setLabel(context))
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    countdown(context, font: .system(.title, design: .rounded).monospacedDigit())
                }
                DynamicIslandExpandedRegion(.bottom) {
                    ProgressView(timerInterval: context.state.startAt...context.state.endAt,
                                 countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
                        .tint(Self.accent)
                }
            } compactLeading: {
                Image(systemName: "timer").foregroundStyle(Self.accent)
            } compactTrailing: {
                countdown(context, font: .caption2.monospacedDigit())
                    .fixedSize()
            } minimal: {
                Image(systemName: "timer").foregroundStyle(Self.accent)
            }
            .keylineTint(Self.accent)
        }
    }

    /// Matches the app's purple accent.
    static let accent = Color(red: 0.66, green: 0.33, blue: 0.97)

    private func setLabel(_ context: ActivityViewContext<RestActivityAttributes>) -> String {
        let a = context.attributes
        let reps = a.targetReps.isEmpty ? "" : " · \(a.targetReps)"
        return "Set \(a.nextSetNumber) of \(a.totalSets)\(reps)"
    }

    private func countdown(_ context: ActivityViewContext<RestActivityAttributes>,
                           font: Font) -> some View {
        Text(timerInterval: context.state.startAt...context.state.endAt, countsDown: true)
            .font(font)
            .monospacedDigit()
            .multilineTextAlignment(.trailing)
            .fixedSize()
    }
}

@available(iOS 16.2, *)
private struct LockScreenView: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Resting")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text(context.attributes.exerciseName)
                        .font(.headline)
                        .lineLimit(1)
                }
                Spacer()
                // Text(timerInterval:) lays out for the widest digits it may need
                // to show; too tight a frame renders as "1:--" rather than shrinking.
                Text(timerInterval: context.state.startAt...context.state.endAt, countsDown: true)
                    .font(.system(.title, design: .rounded).monospacedDigit())
                    .foregroundStyle(RestActivityWidget.accent)
                    .fixedSize()
            }

            ProgressView(timerInterval: context.state.startAt...context.state.endAt,
                         countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
                .tint(RestActivityWidget.accent)

            Text(setLabel)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding()
    }

    private var setLabel: String {
        let a = context.attributes
        let reps = a.targetReps.isEmpty ? "" : " · \(a.targetReps)"
        return "Next: set \(a.nextSetNumber) of \(a.totalSets)\(reps)"
    }
}
