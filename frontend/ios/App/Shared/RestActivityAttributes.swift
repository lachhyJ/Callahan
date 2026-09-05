import ActivityKit
import Foundation

/// One Live Activity per *workout*, not per rest period.
///
/// The activity starts when a session starts and lives until it is finished or
/// discarded, so there is always a card while a workout is open — Skip ends the
/// rest, not the activity. That means everything that changes as you move through
/// the session (exercise, set, rest countdown) has to live in ContentState;
/// Attributes hold only what is fixed for the whole workout.
@available(iOS 16.1, *)
struct RestActivityAttributes: ActivityAttributes, Equatable {

    struct ContentState: Codable, Hashable {
        /// nil when nothing is resting — the card stays up showing the next set.
        var endAt: Date?
        var totalSeconds: Int

        var exerciseName: String
        var targetReps: String
        /// Pre-filled weight for the set you are about to do, already formatted
        /// ("115 kg"). Empty for bodyweight work or a set with nothing carried
        /// over from last time.
        var targetWeight: String
        var nextSetNumber: Int
        var totalSets: Int
        /// Rest length for the set the card is describing, so the Tick button can
        /// start the next rest without waking the webview to ask.
        var restSeconds: Int
        /// Reps typed into the next set's row, when there are any. Distinct from
        /// `targetReps`, which is the programmed prescription and is often a
        /// range ("6-8") — this is the single number sitting in the input.
        var enteredReps: String = ""

        var isResting: Bool { endAt != nil }

        /// Start of the rest period, for the progress bar. Only meaningful while
        /// resting.
        var startAt: Date {
            guard let endAt else { return Date() }
            return endAt.addingTimeInterval(-Double(totalSeconds))
        }

        /// What the next set is loaded to, big enough to read at arm's length:
        /// "35 kg × 6". This replaces the "Go" that used to sit in the middle of
        /// the card once a rest finished — "Go" was a static label with nothing
        /// behind it, so pressing it (reasonably) did nothing, and the numbers you
        /// actually want at that moment were relegated to the small grey line.
        ///
        /// Prefers the reps typed into the row over the programmed target: if you
        /// have already corrected the set to 6, the card should say 6, not "6-8".
        /// Empty when there is nothing loaded (bodyweight, or a fresh exercise) —
        /// callers fall back to the countdown's resting position.
        var loadedSetLine: String {
            let reps = enteredReps.isEmpty ? targetReps : enteredReps
            let parts = [targetWeight, reps].filter { !$0.isEmpty }
            return parts.joined(separator: " × ")
        }

        /// "Next: set 3 of 5 · 115 kg × 6", or a completion note once the last
        /// set is done — the app counts nextSetNumber past totalSets at that
        /// point, and "Next: set 6 of 5" is nonsense to read on a lock screen.
        var nextSetLine: String {
            guard nextSetNumber <= totalSets else { return "Last set done" }
            var line = "Next: set \(nextSetNumber) of \(totalSets)"
            let detail = [targetWeight, targetReps.isEmpty ? "" : "\(targetReps) reps"]
                .filter { !$0.isEmpty }
                .joined(separator: " × ")
            if !detail.isEmpty { line += " · \(detail)" }
            return line
        }
    }

    /// Start of the whole workout, for the elapsed readout in the header. Counts
    /// up for as long as the session is open.
    var sessionStartedAt: Date

    /// Which session this is — "Day A" / "Lower", straight from the template.
    /// The header used to read a generic "Workout", which told you nothing you
    /// did not already know from the dumbbell glyph next to it. Empty for an
    /// ad-hoc session, which falls back to that generic label.
    var templateName: String = ""
    var templateSubtitle: String = ""

    /// Header label: the template's own name, with its subtitle when there is
    /// one. Falls back to "Workout" so a custom session still reads as something.
    var sessionLabel: String {
        let parts = [templateName, templateSubtitle].filter { !$0.isEmpty }
        return parts.isEmpty ? "Workout" : parts.joined(separator: " · ")
    }
}
