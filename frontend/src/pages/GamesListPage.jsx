import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getActivities, getTournaments, createTournament, attachTournamentGames } from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import { formatHoursMinutes } from '../utils/activityLabel'
import { summariseGames } from '../utils/tournamentStats'
import GameRow from '../components/GameRow'

// One-line weekend teaser under a tournament header — null when none of its
// games have field metrics yet, so the header just shows the game count.
function tournamentSummaryLine(games) {
  const s = summariseGames(games)
  if (!s.gamesWithMetrics) return null
  return `${s.totalPoints} pts · ${formatHoursMinutes(s.totalLiveSeconds)} live`
}

export default function GamesListPage() {
  const [games, setGames] = useState(null)
  const [tournaments, setTournaments] = useState(null)
  const [error, setError] = useState(null)

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState(isoDate(new Date()))
  const [endDate, setEndDate] = useState(isoDate(new Date()))
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState(null)
  const [lastAttached, setLastAttached] = useState(null)

  function load() {
    Promise.all([
      getActivities({ type: 'Ultimate', sessionType: 'Game' }),
      getTournaments(),
    ])
      .then(([g, t]) => {
        setGames(g)
        setTournaments(t)
      })
      .catch((err) => setError(err.message))
  }

  useEffect(load, [])

  async function handleCreate(e) {
    e.preventDefault()
    setFormError(null)
    setSaving(true)
    try {
      const tournament = await createTournament({ name, startDate, endDate })
      const { attached } = await attachTournamentGames(tournament.id)
      setLastAttached({ name: tournament.name, attached })
      setShowForm(false)
      setName('')
      load()
    } catch (err) {
      setFormError(err.message)
    } finally {
      setSaving(false)
    }
  }

  if (error) {
    return (
      <main className="page">
        <p className="error">{error}</p>
      </main>
    )
  }

  if (!games || !tournaments) {
    return (
      <main className="page">
        <p>Loading games…</p>
      </main>
    )
  }

  // Group by tournament, in the same reverse-chronological order the games
  // themselves already come back in (GetAll orders by Date desc) - a
  // tournament's position in the list is decided by its most recent game.
  const byTournament = new Map()
  const loose = []
  for (const game of games) {
    if (game.tournamentId == null) {
      loose.push(game)
      continue
    }
    if (!byTournament.has(game.tournamentId)) byTournament.set(game.tournamentId, [])
    byTournament.get(game.tournamentId).push(game)
  }
  const tournamentsById = new Map(tournaments.map((t) => [t.id, t]))
  const sections = [...byTournament.entries()]
    .map(([id, gs]) => ({ tournament: tournamentsById.get(id), games: gs }))
    .filter((s) => s.tournament)
    .sort((a, b) => (a.tournament.startDate < b.tournament.startDate ? 1 : -1))

  return (
    <main className="page">
      <div className="history-week-header">
        <h1>Games</h1>
        <button type="button" className="secondary-btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? 'Cancel' : '+ New tournament'}
        </button>
      </div>

      {showForm && (
        <form onSubmit={handleCreate} className="section-gap">
          <label>
            Name
            <input type="text" value={name} onChange={(e) => setName(e.target.value)} required />
          </label>
          <label>
            Start date
            <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
          </label>
          <label>
            End date
            <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} required />
          </label>
          {formError && <p className="error">{formError}</p>}
          <button type="submit" disabled={saving}>
            {saving ? 'Creating…' : 'Create + attach games'}
          </button>
        </form>
      )}

      {lastAttached && (
        <p className="save-confirm section-gap">
          ✓ {lastAttached.name}: attached {lastAttached.attached} game{lastAttached.attached === 1 ? '' : 's'}
        </p>
      )}

      {games.length === 0 && (
        <div className="empty-state section-gap">
          <p>No games classified as "Game" yet — classify an Ultimate activity from History to see it here.</p>
        </div>
      )}

      <div className="history-week-list section-gap">
        {sections.map(({ tournament, games: gs }) => {
          const summaryLine = tournamentSummaryLine(gs)
          return (
            <div key={tournament.id} className="history-week">
              <Link to={`/tournaments/${tournament.id}`} className="tournament-section-link">
                <div className="history-week-header">
                  <span>{tournament.name} · {formatDateRange(tournament.startDate, tournament.endDate)}</span>
                  <span className="history-week-count">{gs.length} game{gs.length === 1 ? '' : 's'}</span>
                </div>
                {summaryLine && <p className="tournament-section-summary">{summaryLine}</p>}
              </Link>
              {gs.map((g) => <GameRow key={g.id} game={g} />)}
            </div>
          )
        })}

        {loose.length > 0 && (
          <div className="history-week">
            <div className="history-week-header">
              <span>Other games</span>
              <span className="history-week-count">{loose.length} game{loose.length === 1 ? '' : 's'}</span>
            </div>
            {loose.map((g) => <GameRow key={g.id} game={g} />)}
          </div>
        )}
      </div>
    </main>
  )
}
