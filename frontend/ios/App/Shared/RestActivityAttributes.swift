import ActivityKit
import Foundation

/// Shared by the app (which starts, updates and mutates the activity) and the
/// widget extension (which renders it), so the two can never disagree about the
/// shape.
///
/// `endAt` lives in ContentState because -15s/+15s move it; everything describing
/// *which* set you are resting between is fixed for the life of one activity and
/// so lives in the attributes. A different exercise or set ends the activity and
/// starts a new one rather than mutating these.
@available(iOS 16.1, *)
struct RestActivityAttributes: ActivityAttributes, Equatable {

    struct ContentState: Codable, Hashable {
        /// When the rest period ends. The system renders the countdown from this
        /// on its own — we never push per-second updates.
        var endAt: Date
        /// The full rest duration, so the progress bar has a start to measure
        /// from. Derived rather than stored as a start date so that a ± adjust
        /// only has to move one field.
        var totalSeconds: Int

        var startAt: Date { endAt.addingTimeInterval(-Double(totalSeconds)) }
    }

    var exerciseName: String
    var targetReps: String
    /// Pre-filled weight for the set you are about to do, already formatted
    /// ("115 kg"). Empty when the set has no weight — bodyweight work, or a set
    /// with nothing carried over from last time.
    var targetWeight: String
    var nextSetNumber: Int
    var totalSets: Int
    /// Start of the whole workout, for the "28 sec." elapsed readout in the
    /// header. Counts up while the rest timer counts down.
    var sessionStartedAt: Date

    /// "Next: set 3 of 5 · 115 kg × 6", or a completion note once the last set
    /// is done — the app counts nextSetNumber past totalSets at that point, and
    /// "Next: set 6 of 5" is nonsense to read on a lock screen.
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
