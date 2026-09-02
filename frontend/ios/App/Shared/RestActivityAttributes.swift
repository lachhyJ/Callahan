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

        var isResting: Bool { endAt != nil }

        /// Start of the rest period, for the progress bar. Only meaningful while
        /// resting.
        var startAt: Date {
            guard let endAt else { return Date() }
            return endAt.addingTimeInterval(-Double(totalSeconds))
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
}
