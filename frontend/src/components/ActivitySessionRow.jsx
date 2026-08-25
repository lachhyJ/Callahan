import { activityLabel } from '../utils/activityLabel'
import { suggestRunSessionType } from '../utils/runSessionSuggestion'
import { suggestUltimateSessionType } from '../utils/ultimateSessionSuggestion'

function suggestSessionType(activity, typesForActivity) {
  if (activity.type === 'Running') return suggestRunSessionType(activity, typesForActivity)
  if (activity.type === 'Ultimate') return suggestUltimateSessionType(activity, typesForActivity)
  return null
}

// Runs and Ultimate activities both need classifying well after the fact
// (mostly Garmin syncs reviewed during a later browse, not right after
// logging) — this row is shared by both rather than cloned per activity
// type. The label itself is plain text, not a trigger — a dedicated
// Classify/Change button opens the picker (styled bright when classification
// is still needed, so the one button is unambiguously the thing to press),
// and a transparent backdrop closes it on an outside click, so browsing a
// list of activities can't accidentally reclassify one.
export default function ActivitySessionRow({ activity, sessionTypes, openPickerId, onTogglePicker, onSelect }) {
  const pickerOpen = openPickerId === activity.id
  const needsClassification = activity.source === 'Garmin' && !activity.activitySessionTypeId
  const typesForActivity = sessionTypes.filter((t) => t.activityType === activity.type)
  const suggested = suggestSessionType(activity, typesForActivity)

  return (
    <span className="activity-classify">
      <span className="activity-classify-row">
        <span>{activityLabel(activity)}</span>
        <button
          type="button"
          className={needsClassification ? 'activity-classify-btn activity-classify-btn-needed' : 'activity-classify-btn'}
          onClick={() => onTogglePicker(activity.id)}
        >
          {activity.activitySessionTypeId ? 'Change' : 'Classify'}
        </button>
      </span>
      {pickerOpen && (
        <>
          <div className="picker-backdrop" onClick={() => onTogglePicker(activity.id)} />
          <div className="set-type-menu activity-type-menu">
            {typesForActivity.map((t) => (
              <button
                key={t.id}
                type="button"
                className={suggested?.id === t.id ? 'suggested-option' : undefined}
                onClick={() => onSelect(activity.id, t.id)}
              >
                {t.name}
                {suggested?.id === t.id && <span className="suggested-tag">Suggested</span>}
              </button>
            ))}
            {activity.activitySessionTypeId && (
              <button
                type="button"
                className="remove-option"
                onClick={() => {
                  if (window.confirm('Clear this activity’s classification?')) onSelect(activity.id, null)
                }}
              >
                Clear
              </button>
            )}
          </div>
        </>
      )}
    </span>
  )
}
