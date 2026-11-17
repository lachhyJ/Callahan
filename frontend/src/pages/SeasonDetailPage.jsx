import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getActivities, getSeasons, getTournaments } from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import { formatHoursMinutes } from '../utils/format'
import { summariseGames } from '../utils/tournamentStats'
import { seasonContents } from '../utils/ultimateList'
import GameSummaryStats from '../components/GameSummaryStats'
import GameRow from '../components/GameRow'
import { SeasonActions } from '../components/SeasonsSection'

// "3 games · 41 pts · 1h 12m live" for one tournament row; just the count
// when its games have no field data, "upcoming" / "no games" when empty.
function tournamentLine(t, games, today) {
  if (games.length === 0) return t.startDate > today ? 'upcoming' : 'no games'
  const s = summariseGames(games)
  const count = `${games.length} game${games.length === 1 ? '' : 's'}`
  if (!s.gamesWithMetrics) return count
  return `${count} · ${s.totalPoints} pts · ${formatHoursMinutes(s.totalLiveSeconds)} live`
}

// One season's roll-up: totals across every game in it, then its tournaments
// in the order played (reading toward Nationals), then any games outside a
// tournament, with the season's admin last.
export default function SeasonDetailPage() {
  const { seasonId } = useParams()
  const id = Number(seasonId)
  const navigate = useNavigate()
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    Promise.all([
      getSeasons(),
      getTournaments(),
      getActivities({ type: 'Ultimate', sessionType: 'Game' }),
    ])
      .then(([seasons, tournaments, games]) => {
        const season = seasons.find((s) => s.id === id)
        if (!season) {
          setError('Season not found.')
          return
        }
        setData({ season, seasons, tournaments, games })
      })
      .catch((err) => setError(err.message))
  }, [id])

  useEffect(() => {
    setData(null)
    setError(null)
    load()
  }, [load])

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!data) {
    return (
      <main className="page">
        <p>Loading season…</p>
      </main>
    )
  }

  const { season, seasons, tournaments, games } = data
  const today = isoDate(new Date())
  const contents = seasonContents(season, seasons, tournaments, games)
  const allGames = [...contents.tournaments.flatMap((x) => x.games), ...contents.loose]
  const summary = summariseGames(allGames)
  const nationals = season.targetTournamentName ? `Nationals: ${season.targetTournamentName}` : 'No Nationals set'

  return (
    <main className="page">
      <p className="session-date">{formatDateRange(season.startDate, season.endDate)} · {nationals}</p>
      <h1 className="game-title">{season.name}</h1>

      <GameSummaryStats
        summary={summary}
        noun="season"
        extraCards={(
          <div className="stat-card">
            <span className="stat-label">Tournaments</span>
            <span className="stat-value">{contents.tournaments.length}</span>
          </div>
        )}
      />

      <section className="section-gap">
        <h2 className="trend-chart-title">Tournaments</h2>
        {contents.tournaments.length === 0 ? (
          <p className="trend-chart-caption">
            None in this season yet — tournaments inside its dates join it automatically.
          </p>
        ) : (
          <div className="history-week-list">
            {contents.tournaments.map(({ tournament: t, games: gs }) => (
              <Link key={t.id} to={`/tournaments/${t.id}`} className="history-week tournament-section-link">
                <div className="history-week-header">
                  <span>
                    {t.name}
                    {t.id === season.targetTournamentId && <span className="season-target-tag">Nationals</span>}
                  </span>
                  <span className="history-week-count">{formatDateRange(t.startDate, t.endDate)}</span>
                </div>
                <p className="tournament-section-summary">{tournamentLine(t, gs, today)}</p>
              </Link>
            ))}
          </div>
        )}
      </section>

      {contents.loose.length > 0 && (
        <section className="section-gap">
          <h2 className="trend-chart-title">Other games</h2>
          <div className="history-week-list">
            <div className="history-week">
              {contents.loose.map((g) => <GameRow key={g.id} game={g} />)}
            </div>
          </div>
        </section>
      )}

      <section className="section-gap">
        <h2 className="trend-chart-title">Season</h2>
        <SeasonActions
          season={season}
          tournaments={tournaments}
          onChanged={load}
          onDeleted={() => navigate('/ultimate', { replace: true })}
        />
      </section>
    </main>
  )
}
