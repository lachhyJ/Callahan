import { formatHoursMinutes } from '../utils/format'
import FieldSplitBar from './FieldSplitBar'

// Stat cards + field split for a summariseGames() result, shared by the
// tournament and season pages. extraCards go after Games (e.g. a season's
// tournament count). noun names the span in the no-data note.
export default function GameSummaryStats({ summary, noun, extraCards = null }) {
  const hasMetrics = summary.gamesWithMetrics > 0
  return (
    <>
      <div className="stat-grid">
        <div className="stat-card">
          <span className="stat-label">Games</span>
          <span className="stat-value">{summary.gameCount}</span>
        </div>
        {extraCards}
        {hasMetrics && (
          <>
            <div className="stat-card">
              <span className="stat-label">Points played</span>
              <span className="stat-value">{summary.totalPoints}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Live play</span>
              <span className="stat-value">{formatHoursMinutes(summary.totalLiveSeconds)}</span>
            </div>
            {summary.avgLiveMinPerPoint != null && (
              <div className="stat-card">
                <span className="stat-label">Live min / point</span>
                <span className="stat-value">{summary.avgLiveMinPerPoint.toFixed(1)}</span>
              </div>
            )}
            {summary.totalDistanceKm > 0 && (
              <div className="stat-card">
                <span className="stat-label">Distance covered</span>
                <span className="stat-value">{summary.totalDistanceKm.toFixed(1)} km</span>
              </div>
            )}
            {summary.totalLiveDistanceKm > 0 && (
              <div className="stat-card">
                <span className="stat-label">Distance in play</span>
                <span className="stat-value">{summary.totalLiveDistanceKm.toFixed(1)} km</span>
              </div>
            )}
          </>
        )}
      </div>

      {hasMetrics && (
        <FieldSplitBar
          liveSeconds={summary.totalLiveSeconds}
          onFieldSeconds={summary.totalOnFieldSeconds}
          offFieldSeconds={summary.totalOffFieldSeconds}
          mixedSeconds={summary.totalMixedSeconds}
        />
      )}

      {hasMetrics && summary.gamesWithMetrics < summary.gameCount && (
        <p className="game-method-note">
          Field data covers {summary.gamesWithMetrics} of {summary.gameCount} games — the
          rest have no synced GPS track.
        </p>
      )}

      {!hasMetrics && summary.gameCount > 0 && (
        <p className="notes">
          No on/off-field data for this {noun} yet — its games need a synced GPS track.
        </p>
      )}
    </>
  )
}
