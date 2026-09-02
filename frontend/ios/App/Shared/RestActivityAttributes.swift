import ActivityKit
import Foundation

/// Shared by the app (which starts and updates the activity) and the widget
/// extension (which renders it), so the two can never disagree about the shape.
///
/// `endAt` lives in ContentState because adjustRest(±15s) moves it; everything
/// describing *which* set you are resting between is fixed for the life of one
/// activity and so lives in the attributes. A different exercise ends the
/// activity and starts a new one rather than mutating these.
@available(iOS 16.1, *)
struct RestActivityAttributes: ActivityAttributes, Equatable {

    struct ContentState: Codable, Hashable {
        /// When the rest period ends. The system renders the countdown from this
        /// on its own — we never push per-second updates.
        var endAt: Date
        /// The full rest duration, so the progress ring has a start to measure
        /// from. Derived rather than stored as a start date so that a ± adjust
        /// only has to move one field.
        var totalSeconds: Int

        var startAt: Date { endAt.addingTimeInterval(-Double(totalSeconds)) }
    }

    var exerciseName: String
    var targetReps: String
    var nextSetNumber: Int
    var totalSets: Int
}
