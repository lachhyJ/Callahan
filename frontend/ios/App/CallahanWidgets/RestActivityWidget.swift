import ActivityKit
import SwiftUI
import WidgetKit

/// The rest timer on the lock screen and in the Dynamic Island.
///
/// Every countdown here is a `Text(timerInterval:)` / `ProgressView(timerInterval:)`,
/// which the system animates from the start/end dates without the app running.
/// That is the whole point of doing this natively: the web version could not tick
/// while backgrounded because iOS suspends the webview's JS.
///
/// Sizing rule learned the hard way: never `.fixedSize()` a `Text(timerInterval:)`
/// here. Inside the ProgressView's GeometryReader it trips a SwiftUI layout
/// assertion and crashes the widget process (EXC_BREAKPOINT in
/// LayoutSubview.place), which silently stops the lock-screen card rendering
/// while the minimal presentation — an Image, no Text — carries on working.
/// Equally, do not cap it with `maxWidth`: the view lays out for the widest
/// digits it might show and renders "1:--" rather than shrinking. Give it a
/// generous minimum and let the stack place it.
@available(iOS 16.2, *)
struct RestActivityWidget: Widget {

    var body: some WidgetConfiguration {
        ActivityConfiguration(for: RestActivityAttributes.self) { context in
            LockScreenView(context: context)
                .activityBackgroundTint(.black.opacity(0.6))
                .activitySystemActionForegroundColor(.white)

        } dynamicIsland: { context in
            DynamicIsland {
                // The leading/trailing regions are narrow — an exercise name does
                // not fit and truncates to "Trap Bar Dea...". Keep them to a label
                // and the clock, and give the name the full-width bottom region.
                DynamicIslandExpandedRegion(.leading) {
                    Label("Resting", systemImage: "timer")
                        .font(.caption)
                        .foregroundStyle(Self.accent)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    Text(timerInterval: context.state.startAt...context.state.endAt,
                         countsDown: true)
                        .font(.system(.title3, design: .rounded).monospacedDigit())
                        .foregroundStyle(Self.accent)
                        .lineLimit(1)
                        .frame(minWidth: 64, alignment: .trailing)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(context.attributes.exerciseName)
                            .font(.headline)
                            .lineLimit(1)
                            .minimumScaleFactor(0.8)
                        progressBar(context)
                        Text(setLabel(context))
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            } compactLeading: {
                Image(systemName: "timer").foregroundStyle(Self.accent)
            } compactTrailing: {
                Text(timerInterval: context.state.startAt...context.state.endAt,
                     countsDown: true)
                    .font(.caption2.monospacedDigit())
                    .foregroundStyle(Self.accent)
                    .lineLimit(1)
                    .frame(minWidth: 40, alignment: .trailing)
            } minimal: {
                Image(systemName: "timer").foregroundStyle(Self.accent)
            }
            .keylineTint(Self.accent)
        }
    }

    /// Matches the app's purple accent.
    static let accent = Color(red: 0.66, green: 0.33, blue: 0.97)

    fileprivate static func setLabelText(_ a: RestActivityAttributes) -> String {
        let reps = a.targetReps.isEmpty ? "" : " · \(a.targetReps)"
        return "Set \(a.nextSetNumber) of \(a.totalSets)\(reps)"
    }

    private func setLabel(_ context: ActivityViewContext<RestActivityAttributes>) -> String {
        Self.setLabelText(context.attributes)
    }

    private func progressBar(_ context: ActivityViewContext<RestActivityAttributes>) -> some View {
        ProgressView(timerInterval: context.state.startAt...context.state.endAt,
                     countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
            .tint(Self.accent)
    }
}

@available(iOS 16.2, *)
private struct LockScreenView: View {
    let context: ActivityViewContext<RestActivityAttributes>

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .lastTextBaseline, spacing: 12) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("RESTING")
                        .font(.caption2.weight(.semibold))
                        .tracking(0.8)
                        .foregroundStyle(RestActivityWidget.accent)
                    Text(context.attributes.exerciseName)
                        .font(.title3.weight(.semibold))
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                }

                // Spacer does the right-alignment; no maxWidth, or the timer
                // renders as "1:--", and no fixed width, or it floats mid-card.
                Spacer(minLength: 8)

                // layoutPriority matters: without it the exercise name claims the
                // width first, the timer is squeezed under its ideal size, and it
                // renders "2:--" instead of shrinking. The name absorbs the squeeze
                // instead — it has lineLimit(1) and minimumScaleFactor to do so.
                Text(timerInterval: context.state.startAt...context.state.endAt,
                     countsDown: true)
                    .font(.system(size: 32, weight: .semibold, design: .rounded).monospacedDigit())
                    .foregroundStyle(RestActivityWidget.accent)
                    .lineLimit(1)
                    .layoutPriority(1)
                    .frame(minWidth: 96, alignment: .trailing)
            }

            ProgressView(timerInterval: context.state.startAt...context.state.endAt,
                         countsDown: true) { EmptyView() } currentValueLabel: { EmptyView() }
                .tint(RestActivityWidget.accent)

            Text(RestActivityWidget.setLabelText(context.attributes))
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }
}
