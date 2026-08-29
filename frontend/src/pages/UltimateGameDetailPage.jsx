import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getActivity, getActivityFieldTimeline, getTournaments, updateActivityTournament } from '../api/client'
import { activityLabel, formatDuration } from '../utils/activityLabel'
import { formatDateLong } from '../dateUtils'
import FieldTimeline from '../components/FieldTimeline'
import FieldSplitBar from '../components/FieldSplitBar'

// How each LapFieldClassifier.Method value should read to someone who isn't
// the one who built it — the honesty line under the stats, since
// GeometryFromLaps has never run on real data yet (see backend/decisions).
const METHOD_LABELS = {
  LabelledFromGarmin: 'from Garmin’s own structured-run labels',
  GeometryNoLaps: 'from GPS geometry',
  GeometryFromLaps: 'from lap presses + GPS geometry',
  NoTrack: 'no GPS track synced yet',
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
  // "Live play" = on-field time inside a detected point. The rest of on-field
  // time (waiting on the line between points, subbing on) is shown separately
  // by FieldSplitBar.
  const liveSeconds = hasFieldData ? (activity.livePlaySeconds ?? 0) : 0
  const livePerPoint = hasFieldData && activity.pointsPlayed && liveSeconds
    ? (liveSeconds / 60 / activity.pointsPlayed).toFixed(1)
    : null

  return (
    <main className="page">
      <p className="session-date">{formatDateLong(activity.date)}</p>
      <h1 className="game-title">{activityLabel(activity)}</h1>

      {activity.type === 'Ultimate' && (
        <span className="activity-classify">
          {activity.tournamentId ? (
            <Link to={`/tournaments/${activity.tournamentId}`} className="activity-classify-link">{activity.tournamentName}</Link>
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
          <FieldSplitBar
            liveSeconds={liveSeconds}
            onFieldSeconds={activity.onFieldSeconds}
            offFieldSeconds={activity.offFieldSeconds}
            mixedSeconds={activity.mixedSeconds ?? 0}
          />

          <div className="game-stat-row">
            {activity.pointsPlayed != null && (
              <div className="stat-card">
                <span className="stat-label">Points played</span>
                <span className="stat-value">{activity.pointsPlayed}</span>
              </div>
            )}
            {livePerPoint != null && (
              <div className="stat-card">
                <span className="stat-label">Live min / point</span>
                <span className="stat-value">{livePerPoint}</span>
              </div>
            )}
            {activity.distanceKm != null && (
              <div className="stat-card">
                <span className="stat-label">Distance covered</span>
                <span className="stat-value">{Number(activity.distanceKm).toFixed(2)} km</span>
              </div>
            )}
            {activity.livePlayDistanceKm != null && (
              <div className="stat-card">
                <span className="stat-label">Distance in play</span>
                <span className="stat-value">{Number(activity.livePlayDistanceKm).toFixed(2)} km</span>
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
