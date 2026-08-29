import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getActivities, getTournaments, createTournament, attachTournamentGames } from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import { formatHoursMinutes } from '../utils/activityLabel'
import { summariseGames } from '../utils/tournamentStats'
import GameRow from '../components/GameRow'
import SyncGarminButton from '../components/SyncGarminButton'

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

  // One date-ordered list mixing tournament sections with standalone games,
  // rather than a separate bucket for the loose ones. GetAll returns games
  // Date desc, so gs[0] is a tournament's most recent game and its date
  // decides where the section sits relative to a standalone game.
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
  const entries = [
    ...[...byTournament.entries()]
      .map(([id, gs]) => ({ kind: 'tournament', tournament: tournamentsById.get(id), games: gs }))
      .filter((e) => e.tournament)
      .map((e) => ({ ...e, sortDate: e.games[0].date })),
    ...loose.map((g) => ({ kind: 'game', game: g, sortDate: g.date })),
  ].sort((a, b) => (a.sortDate < b.sortDate ? 1 : a.sortDate > b.sortDate ? -1 : 0))

  return (
    <main className="page">
      <div className="history-week-header">
        <h1>Games</h1>
        <div className="games-header-actions">
          <SyncGarminButton onSynced={load} />
          <button type="button" className="secondary-btn" onClick={() => setShowForm((v) => !v)}>
            {showForm ? 'Cancel' : '+ New tournament'}
          </button>
        </div>
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
        {entries.map((entry) => {
          if (entry.kind === 'game') {
            return (
              <div key={`g-${entry.game.id}`} className="history-week">
                <GameRow game={entry.game} />
              </div>
            )
          }
          const { tournament, games: gs } = entry
          const summaryLine = tournamentSummaryLine(gs)
          return (
            <div key={`t-${tournament.id}`} className="history-week">
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
      </div>
    </main>
  )
}
