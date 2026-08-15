import { activityLabel } from '../utils/activityLabel'

// Runs need classifying well after the fact (mostly Garmin syncs reviewed
// during a later browse, not right after logging). The label itself is
// plain text, not a trigger — a dedicated Classify/Change button opens the
// picker, and a transparent backdrop closes it on an outside click, so
// browsing a list of runs can't accidentally reclassify one.
export default function RunActivityRow({ activity, runSessionTypes, openPickerId, onTogglePicker, onSelect }) {
  const pickerOpen = openPickerId === activity.id
  const needsClassification = activity.source === 'Garmin' && !activity.runSessionTypeId

  return (
    <span className="run-classify">
      <span className="run-classify-row">
        <span>{activityLabel(activity)}</span>
        <button type="button" className="run-classify-btn" onClick={() => onTogglePicker(activity.id)}>
          {activity.runSessionTypeId ? 'Change' : 'Classify'}
        </button>
        {needsClassification && <span className="needs-classification-badge">Needs classification</span>}
      </span>
      {pickerOpen && (
        <>
          <div className="picker-backdrop" onClick={() => onTogglePicker(activity.id)} />
          <div className="set-type-menu run-type-menu">
            {runSessionTypes.map((t) => (
              <button key={t.id} type="button" onClick={() => onSelect(activity.id, t.id)}>
                {t.name}
              </button>
            ))}
            {activity.runSessionTypeId && (
              <button type="button" className="remove-option" onClick={() => onSelect(activity.id, null)}>
                Clear
              </button>
            )}
          </div>
        </>
      )}
    </span>
  )
}
