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

// The activity's current tag set as a plain id list + primary id. Falls back
// to the single primary field for a DTO from before multi-tag (sessionTypes
// absent). Primary first is guaranteed by the API, but we key off the explicit
// primary id rather than list position.
function currentTags(activity) {
  const list = activity.sessionTypes?.length
    ? activity.sessionTypes
    : activity.activitySessionTypeId
      ? [{ id: activity.activitySessionTypeId, name: activity.activitySessionTypeName }]
      : []
  return {
    ids: list.map((t) => t.id),
    primary: activity.activitySessionTypeId ?? list[0]?.id ?? null,
  }
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
        <span className="activity-classify-stat">GPS estimate: {activity.highSpeedDistanceKm.toFixed(2)} km</span>
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
//
// An activity can hold several session-type labels (a field session with a
// throwing block on the same Garmin recording). The picker is a multi-select:
// tick every type that applies, one is marked primary (drives the label and
// the Game analysis gate), and nothing is written until "Done".
export default function ActivitySessionRow({ activity, sessionTypes, openPickerId, onTogglePicker, onSave, onConeDistanceChange }) {
  const pickerOpen = openPickerId === activity.id
  const needsClassification = activity.source === 'Garmin' && !activity.activitySessionTypeId
  // Field-family types are cross-type: a field session gets recorded sometimes
  // as a run, sometimes as an Ultimate, so offer them either way.
  const typesForActivity = sessionTypes.filter((t) => t.activityType === activity.type || t.family === 'Field')
  const suggested = suggestSessionType(activity, typesForActivity)
  const isHighSpeedIntervals = activity.activitySessionTypeName === HIGH_SPEED_INTERVALS_TYPE_NAME
  const teaser = livePlayTeaser(activity)

  // Draft tag set, live only while the menu is open. Reset from the activity
  // on open, cleared on close, via a state adjustment during render (keyed on
  // which activity it's for) so an in-progress edit survives a parent
  // re-render but a reopen starts fresh.
  const [draft, setDraft] = useState({ forId: null, ids: [], primary: null })
  if (pickerOpen && draft.forId !== activity.id) {
    setDraft({ forId: activity.id, ...currentTags(activity) })
  } else if (!pickerOpen && draft.forId !== null) {
    setDraft({ forId: null, ids: [], primary: null })
  }

  const extraNames = (activity.sessionTypes ?? []).filter((t) => t.id !== activity.activitySessionTypeId).map((t) => t.name)

  function toggle(id) {
    setDraft((d) => {
      if (d.ids.includes(id)) {
        const ids = d.ids.filter((x) => x !== id)
        const primary = d.primary === id
          ? typesForActivity.find((t) => ids.includes(t.id))?.id ?? null
          : d.primary
        return { ...d, ids, primary }
      }
      return { ...d, ids: [...d.ids, id], primary: d.primary ?? id }
    })
  }

  function makePrimary(id) {
    setDraft((d) => (d.ids.includes(id) ? { ...d, primary: id } : d))
  }

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
        {extraNames.length > 0 && (
          <span className="activity-classify-extra"> · {extraNames.join(', ')}</span>
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
          <div className="set-type-menu activity-type-menu activity-type-menu-multi">
            {typesForActivity.map((t) => {
              const checked = draft.ids.includes(t.id)
              const isPrimary = draft.primary === t.id
              return (
                <label key={t.id} className={checked ? 'multi-option is-checked' : 'multi-option'}>
                  <input type="checkbox" checked={checked} onChange={() => toggle(t.id)} />
                  <span className="multi-option-name">{t.name}</span>
                  {suggested?.id === t.id && !checked && <span className="suggested-tag">Suggested</span>}
                  {checked && (
                    <button
                      type="button"
                      className={isPrimary ? 'primary-toggle is-primary' : 'primary-toggle'}
                      onClick={() => makePrimary(t.id)}
                      aria-label={isPrimary ? `${t.name} is the primary type` : `Make ${t.name} the primary type`}
                    >
                      {isPrimary ? 'Primary' : 'Make primary'}
                    </button>
                  )}
                </label>
              )
            })}
            <div className="multi-actions">
              {draft.ids.length > 0 && (
                <button
                  type="button"
                  className="remove-option"
                  onClick={() => onSave(activity.id, { primaryId: null, typeIds: [] })}
                >
                  Clear
                </button>
              )}
              <button
                type="button"
                className="multi-done"
                onClick={() => onSave(activity.id, { primaryId: draft.primary, typeIds: draft.ids })}
              >
                Done
              </button>
            </div>
          </div>
        </>
      )}
    </span>
  )
}
