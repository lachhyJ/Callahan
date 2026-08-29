import { useState } from 'react'
import { Link } from 'react-router-dom'
import { activityLabel, livePlayTeaser } from '../utils/activityLabel'
import { suggestRunSessionType } from '../utils/runSessionSuggestion'
import { suggestUltimateSessionType } from '../utils/ultimateSessionSuggestion'

function suggestSessionType(activity, typesForActivity) {
  if (activity.type === 'Running') return suggestRunSessionType(activity, typesForActivity)
  if (activity.type === 'Ultimate') return suggestUltimateSessionType(activity, typesForActivity)
  return null
}

// Laps only ever come from Garmin's HS-Intervals-labeled sessions today, so
// this is a name match rather than reading activity.type - matches the
// session-type list's own naming (backend/Data/AppDbContext.cs seed data).
const HIGH_SPEED_INTERVALS_TYPE_NAME = 'High Speed Intervals'

// Cone spacing is a fixed number Lachlan paces out himself before a session
// - GPS/lap data can't give it directly (shuttle turns make GPS distance an
// underestimate on this kind of session). Shown alongside the lap-derived
// HighSpeedDistanceKm rather than combined into one number, since they're
// independent measurements and neither should quietly override the other.
function ConeDistanceInput({ activity, onConeDistanceChange }) {
  const [value, setValue] = useState(activity.coneDistanceM ?? '')

  function commit() {
    const parsed = value === '' ? null : Number(value)
    if (parsed === (activity.coneDistanceM ?? null)) return
    onConeDistanceChange(activity.id, Number.isFinite(parsed) ? parsed : null)
  }

  return (
    <span className="activity-classify-detail">
      {activity.highSpeedDistanceKm != null && (
        <span className="activity-classify-stat">GPS high-speed: {activity.highSpeedDistanceKm} km</span>
      )}
      <label className="activity-classify-cone">
        Cones (m)
        <input
          type="number"
          inputMode="numeric"
          min="0"
          placeholder="—"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={commit}
        />
      </label>
    </span>
  )
}

// Runs and Ultimate activities both need classifying well after the fact
// (mostly Garmin syncs reviewed during a later browse, not right after
// logging) — this row is shared by both rather than cloned per activity
// type. The label itself is plain text, not a trigger — a dedicated
// Classify/Change button opens the picker (styled bright when classification
// is still needed, so the one button is unambiguously the thing to press),
// and a transparent backdrop closes it on an outside click, so browsing a
// list of activities can't accidentally reclassify one.
export default function ActivitySessionRow({ activity, sessionTypes, openPickerId, onTogglePicker, onSelect, onConeDistanceChange }) {
  const pickerOpen = openPickerId === activity.id
  const needsClassification = activity.source === 'Garmin' && !activity.activitySessionTypeId
  const typesForActivity = sessionTypes.filter((t) => t.activityType === activity.type)
  const suggested = suggestSessionType(activity, typesForActivity)
  const isHighSpeedIntervals = activity.activitySessionTypeName === HIGH_SPEED_INTERVALS_TYPE_NAME
  const teaser = livePlayTeaser(activity)

  return (
    <span className="activity-classify">
      <span className="activity-classify-row">
        {activity.type === 'Ultimate' ? (
          <Link to={`/activities/${activity.id}`} className="activity-classify-link">
            {activityLabel(activity)}
            {teaser && <span className="activity-classify-teaser"> · {teaser}</span>}
          </Link>
        ) : (
          <span>{activityLabel(activity)}</span>
        )}
        <button
          type="button"
          className={needsClassification ? 'activity-classify-btn activity-classify-btn-needed' : 'activity-classify-btn'}
          onClick={() => onTogglePicker(activity.id)}
        >
          {activity.activitySessionTypeId ? 'Change' : 'Classify'}
        </button>
      </span>
      {isHighSpeedIntervals && onConeDistanceChange && (
        <ConeDistanceInput activity={activity} onConeDistanceChange={onConeDistanceChange} />
      )}
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
