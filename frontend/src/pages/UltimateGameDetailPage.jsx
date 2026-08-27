import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getActivity, getActivityFieldTimeline, getTournaments, updateActivityTournament } from '../api/client'
import { activityLabel, formatDuration } from '../utils/activityLabel'
import { formatDateLong } from '../dateUtils'
import FieldTimeline from '../components/FieldTimeline'

// How each LapFieldClassifier.Method value should read to someone who isn't
// the one who built it — the honesty line under the stats, since
// GeometryFromLaps has never run on real data yet (see backend/decisions).
const METHOD_LABELS = {
  LabelledFromGarmin: 'from Garmin’s own structured-run labels',
  GeometryNoLaps: 'from GPS geometry',
  GeometryFromLaps: 'from lap presses + GPS geometry',
  NoTrack: 'no GPS track synced yet',
}

function formatHoursMinutes(totalSeconds) {
  const minutes = Math.round(totalSeconds / 60)
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return hours > 0 ? `${hours}h ${mins}min` : `${mins}min`
}

function formatPercent(part, whole) {
  if (!whole) return null
  return Math.round((part / whole) * 100)
}

export default function UltimateGameDetailPage() {
  const { activityId } = useParams()
  const [activity, setActivity] = useState(null)
  const [timeline, setTimeline] = useState(null)
  const [error, setError] = useState(null)
  const [tournaments, setTournaments] = useState(null)
  const [pickerOpen, setPickerOpen] = useState(false)

  useEffect(() => {
    setActivity(null)
    setTimeline(null)
    setError(null)
    getActivity(activityId).then(setActivity).catch((err) => setError(err.message))
  }, [activityId])

  // Own effect, silent catch — the timeline is a spot-check extra, never
  // allowed to block or blank the aggregate stats above it.
  useEffect(() => {
    if (!activity || activity.onFieldSeconds == null) return
    getActivityFieldTimeline(activityId).then(setTimeline).catch(() => {})
  }, [activity, activityId])

  // Only fetched when the picker is opened, not on every page load - the
  // tournament list is small but there's no reason to pull it for a page
  // view that never touches it.
  function openPicker() {
    setPickerOpen(true)
    if (!tournaments) getTournaments().then(setTournaments).catch(() => {})
  }

  async function handleSelectTournament(tournamentId) {
    setPickerOpen(false)
    const updated = await updateActivityTournament(activityId, tournamentId)
    setActivity(updated)
  }

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!activity) {
    return (
      <main className="page">
        <p>Loading activity…</p>
      </main>
    )
  }

  const hasFieldData = activity.onFieldSeconds != null
  const totalTracked = hasFieldData
    ? activity.onFieldSeconds + activity.offFieldSeconds + (activity.mixedSeconds ?? 0)
    : 0
  const onPct = hasFieldData ? formatPercent(activity.onFieldSeconds, totalTracked) : null
  const offPct = hasFieldData ? formatPercent(activity.offFieldSeconds, totalTracked) : null
  const mixedPct = hasFieldData && activity.mixedSeconds > 0 ? formatPercent(activity.mixedSeconds, totalTracked) : null
  const minPerPoint = hasFieldData && activity.pointsPlayed
    ? (activity.onFieldSeconds / 60 / activity.pointsPlayed).toFixed(1)
    : null

  return (
    <main className="page">
      <p className="session-date">{formatDateLong(activity.date)}</p>
      <h1 className="game-title">{activityLabel(activity)}</h1>

      {activity.type === 'Ultimate' && (
        <span className="activity-classify">
          {activity.tournamentId ? (
            <Link to="/games" className="activity-classify-link">{activity.tournamentName}</Link>
          ) : (
            <span className="activity-classify-teaser">No tournament</span>
          )}
          <button type="button" className="activity-classify-btn" onClick={openPicker}>
            {activity.tournamentId ? 'Change' : 'Set tournament'}
          </button>
          {pickerOpen && (
            <>
              <div className="picker-backdrop" onClick={() => setPickerOpen(false)} />
              <div className="set-type-menu">
                {tournaments === null && <span className="picker-menu-note">Loading…</span>}
                {tournaments?.length === 0 && <span className="picker-menu-note">No tournaments yet</span>}
                {tournaments?.map((t) => (
                  <button key={t.id} type="button" onClick={() => handleSelectTournament(t.id)}>
                    {t.name}
                  </button>
                ))}
                {activity.tournamentId && (
                  <button type="button" className="remove-option" onClick={() => handleSelectTournament(null)}>
                    Clear
                  </button>
                )}
              </div>
            </>
          )}
        </span>
      )}

      {!hasFieldData && (
        <p className="notes">
          {activity.type === 'Ultimate'
            ? 'No on/off-field data for this activity yet — it needs to be classified as “Game” with a synced GPS track.'
            : `${formatDuration(activity.durationSeconds)} logged.`}
        </p>
      )}

      {hasFieldData && (
        <>
          <div className="field-split-bar">
            <div className="field-split-segment field-split-on" style={{ width: `${onPct}%` }} />
            {mixedPct != null && <div className="field-split-segment field-split-mixed" style={{ width: `${mixedPct}%` }} />}
            <div className="field-split-segment field-split-off" style={{ width: `${offPct}%` }} />
          </div>
          <div className="field-split-legend">
            <span className="field-split-legend-item">
              <i className="field-timeline-swatch field-timeline-swatch-on" />
              On field {formatHoursMinutes(activity.onFieldSeconds)} · {onPct}%
            </span>
            {mixedPct != null && (
              <span className="field-split-legend-item">
                <i className="field-timeline-swatch field-timeline-swatch-mixed" />
                Mixed {formatHoursMinutes(activity.mixedSeconds)} · {mixedPct}%
              </span>
            )}
            <span className="field-split-legend-item">
              <i className="field-timeline-swatch field-timeline-swatch-off" />
              Off field {formatHoursMinutes(activity.offFieldSeconds)} · {offPct}%
            </span>
          </div>
          <p className="field-split-note">
            Time spent physically on the field — includes waiting on the line
            between points, stall counts and subbing on, not only live play.
          </p>

          <div className="game-stat-row">
            {activity.pointsPlayed != null && (
              <div className="stat-card">
                <span className="stat-label">Points played</span>
                <span className="stat-value">{activity.pointsPlayed}</span>
              </div>
            )}
            {minPerPoint != null && (
              <div className="stat-card">
                <span className="stat-label">Min / point</span>
                <span className="stat-value">{minPerPoint}</span>
              </div>
            )}
            {activity.onFieldDistanceKm != null && (
              <div className="stat-card">
                <span className="stat-label">On-field distance</span>
                <span className="stat-value">{Number(activity.onFieldDistanceKm).toFixed(2)} km</span>
              </div>
            )}
          </div>

          <FieldTimeline timeline={timeline} />

          {activity.lapClassifierMethod && (
            <p className="game-method-note">
              Computed {METHOD_LABELS[activity.lapClassifierMethod] ?? activity.lapClassifierMethod}
              {activity.trackSampleCount > 0 && ` · ${activity.trackSampleCount} GPS samples`}
              {activity.alternationViolations > 0 && ` · ${activity.alternationViolations} alternation violation${activity.alternationViolations === 1 ? '' : 's'}`}
            </p>
          )}
        </>
      )}
    </main>
  )
}
