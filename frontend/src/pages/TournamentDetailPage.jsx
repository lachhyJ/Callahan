import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getActivities, getTournaments } from '../api/client'
import { formatDateRange, formatDateMedium } from '../dateUtils'
import { formatHoursMinutes } from '../utils/activityLabel'
import { summariseGames, groupByDay } from '../utils/tournamentStats'
import FieldSplitBar from '../components/FieldSplitBar'
import TournamentGameChart from '../components/TournamentGameChart'
import GameRow from '../components/GameRow'

// Roll-up for one tournament weekend. Ordering is deliberate: weekend totals
// first, per-day load second, game-by-game last — the individual-performance
// cut sits below the weekend view rather than being the first thing seen.
export default function TournamentDetailPage() {
  const { tournamentId } = useParams()
  const id = Number(tournamentId)
  const [games, setGames] = useState(null)
  const [tournament, setTournament] = useState(null)
  const [error, setError] = useState(null)
  const [chartMode, setChartMode] = useState('totals')

  useEffect(() => {
    setGames(null)
    setTournament(null)
    setError(null)
    Promise.all([
      getActivities({ type: 'Ultimate', sessionType: 'Game' }),
      getTournaments(),
    ])
      .then(([allGames, tournaments]) => {
        const t = tournaments.find((x) => x.id === id)
        if (!t) {
          setError('Tournament not found.')
          return
        }
        setTournament(t)
        setGames(
          allGames
            .filter((g) => g.tournamentId === id)
            .sort((a, b) => (a.date < b.date ? -1 : a.date > b.date ? 1 : a.id - b.id)),
        )
      })
      .catch((err) => setError(err.message))
  }, [id])

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!games || !tournament) {
    return (
      <main className="page">
        <p>Loading tournament…</p>
      </main>
    )
  }

  const summary = summariseGames(games)
  const days = groupByDay(games)
  const hasMetrics = summary.gamesWithMetrics > 0

  const chartPoints = games
    .filter((g) => g.livePlaySeconds != null && (chartMode === 'totals' || g.pointsPlayed))
    .map((g) => ({
      date: g.date,
      value:
        chartMode === 'totals'
          ? g.livePlaySeconds / 60
          : g.livePlaySeconds / 60 / g.pointsPlayed,
    }))
  const chartUnit = chartMode === 'totals' ? 'min live' : 'min/pt'

  return (
    <main className="page">
      <p className="session-date">{formatDateRange(tournament.startDate, tournament.endDate)}</p>
      <h1 className="game-title">{tournament.name}</h1>

      <div className="stat-grid">
        <div className="stat-card">
          <span className="stat-label">Games</span>
          <span className="stat-value">{summary.gameCount}</span>
        </div>
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

      {!hasMetrics && (
        <p className="notes">
          No on/off-field data for this tournament yet — its games need a synced GPS track.
        </p>
      )}

      {days.length > 1 && (
        <section className="section-gap">
          <h2 className="trend-chart-title">By day</h2>
          <div className="history-week-list">
            {days.map((d) => (
              <div key={d.date} className="history-week">
                <div className="history-week-header">
                  <span>{formatDateMedium(d.date)}</span>
                  <span className="history-week-count">
                    {d.summary.gameCount} game{d.summary.gameCount === 1 ? '' : 's'}
                  </span>
                </div>
                <div className="tournament-day-stats">
                  {d.summary.gamesWithMetrics > 0 ? (
                    <>
                      <span>{d.summary.totalPoints} pts</span>
                      <span>{formatHoursMinutes(d.summary.totalLiveSeconds)} live</span>
                      {d.summary.avgLiveMinPerPoint != null && (
                        <span>{d.summary.avgLiveMinPerPoint.toFixed(1)} min/pt</span>
                      )}
                    </>
                  ) : (
                    <span>No field data</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="section-gap">
        <h2 className="trend-chart-title">Game by game</h2>
        {chartPoints.length >= 2 && (
          <>
            <div className="unit-toggle-row">
              <button
                type="button"
                className={`secondary-btn${chartMode === 'totals' ? ' active' : ''}`}
                onClick={() => setChartMode('totals')}
              >
                Totals
              </button>
              <button
                type="button"
                className={`secondary-btn${chartMode === 'rates' ? ' active' : ''}`}
                onClick={() => setChartMode('rates')}
              >
                Per point
              </button>
            </div>
            <TournamentGameChart points={chartPoints} unitLabel={chartUnit} />
          </>
        )}
        <div className="history-week-list section-gap">
          <div className="history-week">
            {games.map((g) => (
              <GameRow key={g.id} game={g} />
            ))}
          </div>
        </div>
      </section>
    </main>
  )
}
