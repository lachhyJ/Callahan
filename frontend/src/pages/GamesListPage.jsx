import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getActivities,
  getTournaments,
  createTournament,
  updateTournament,
  deleteTournament,
  attachTournamentGames,
} from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import { formatHoursMinutes } from '../utils/activityLabel'
import { summariseGames } from '../utils/tournamentStats'
import GameRow from '../components/GameRow'
import SeasonsSection from '../components/SeasonsSection'
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
  // null while creating, a tournament id while editing that one.
  const [editingId, setEditingId] = useState(null)
  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState(isoDate(new Date()))
  const [endDate, setEndDate] = useState(isoDate(new Date()))
  // '' means no taper. Kept as a string so the number input can be emptied.
  const [taperDays, setTaperDays] = useState('')
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

  function startCreate() {
    setEditingId(null)
    setName('')
    setStartDate(isoDate(new Date()))
    setEndDate(isoDate(new Date()))
    setTaperDays('')
    setFormError(null)
    setShowForm(true)
  }

  function startEdit(t) {
    setEditingId(t.id)
    setName(t.name)
    setStartDate(t.startDate)
    setEndDate(t.endDate)
    setTaperDays(t.taperDays == null ? '' : String(t.taperDays))
    setFormError(null)
    setShowForm(true)
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setSaving(true)
    try {
      const fields = {
        name,
        startDate,
        endDate,
        taperDays: taperDays === '' ? null : Number(taperDays),
      }
      if (editingId == null) {
        const tournament = await createTournament(fields)
        const { attached } = await attachTournamentGames(tournament.id)
        setLastAttached({ name: tournament.name, attached })
      } else {
        // The season link isn't editable here - it's owned by Seasons above,
        // which assigns tournaments by date range. Passing the existing value
        // through keeps this form from clearing it.
        const existing = tournaments.find((t) => t.id === editingId)
        await updateTournament(editingId, { ...fields, seasonId: existing?.seasonId ?? null })
        setLastAttached(null)
      }
      setShowForm(false)
      setEditingId(null)
      setName('')
      load()
    } catch (err) {
      setFormError(err.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(t) {
    const gameCount = t.gameCount ?? 0
    const consequences = [
      gameCount > 0 ? `Its ${gameCount} game${gameCount === 1 ? '' : 's'} stay, just ungrouped.` : null,
      t.taperDays != null ? 'Its taper check-ins are deleted.' : null,
    ].filter(Boolean).join(' ')
    if (!window.confirm(`Delete ${t.name}? ${consequences}`)) return
    try {
      await deleteTournament(t.id)
      setLastAttached(null)
      load()
    } catch (err) {
      setError(err.message)
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
  // Date desc, so gs[0] is a tournament's most recent game.
  //
  // Sections are built from the tournament list, not from the games grouping:
  // a tournament with no games yet is exactly what the taper page creates for
  // an upcoming weekend, and it has to be visible here to be edited at all.
  // Such a section sorts on its own StartDate, so an upcoming tournament sits
  // at the top where it belongs.
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
  const entries = [
    ...tournaments.map((t) => {
      const gs = byTournament.get(t.id) ?? []
      return { kind: 'tournament', tournament: t, games: gs, sortDate: gs[0]?.date ?? t.startDate }
    }),
    ...loose.map((g) => ({ kind: 'game', game: g, sortDate: g.date })),
  ].sort((a, b) => (a.sortDate < b.sortDate ? 1 : a.sortDate > b.sortDate ? -1 : 0))

  return (
    <main className="page">
      <div className="history-week-header">
        <h1>Ultimate</h1>
        <div className="games-header-actions">
          <SyncGarminButton onSynced={load} />
          <button
            type="button"
            className="secondary-btn"
            onClick={showForm ? () => { setShowForm(false); setEditingId(null) } : startCreate}
          >
            {showForm ? 'Cancel' : '+ New tournament'}
          </button>
        </div>
      </div>

      <SeasonsSection tournaments={tournaments} />

      {showForm && (
        <form onSubmit={handleSubmit} className="section-gap">
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
          <label>
            Taper length (days)
            <input
              type="number"
              min="1"
              max="30"
              value={taperDays}
              onChange={(e) => setTaperDays(e.target.value)}
              placeholder="leave blank for no taper"
            />
          </label>
          <p className="trend-chart-caption">
            Set a taper length to have this tournament drive the Taper page. Past tournaments
            you're only recording games for should leave it blank.
          </p>
          {formError && <p className="error">{formError}</p>}
          <button type="submit" disabled={saving}>
            {saving ? 'Saving…' : editingId == null ? 'Create + attach games' : 'Save changes'}
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
                  <span className="history-week-count">
                    {gs.length === 0 ? 'no games yet' : `${gs.length} game${gs.length === 1 ? '' : 's'}`}
                  </span>
                </div>
                {summaryLine && <p className="tournament-section-summary">{summaryLine}</p>}
              </Link>
              {tournament.taperDays != null && (
                <p className="tournament-section-summary">
                  Tapering {tournament.taperDays} days into this one.
                </p>
              )}
              {gs.map((g) => <GameRow key={g.id} game={g} />)}
              <div className="games-header-actions">
                <button type="button" className="secondary-btn" onClick={() => startEdit(tournament)}>Edit</button>
                <button type="button" className="secondary-btn" onClick={() => handleDelete(tournament)}>Delete</button>
              </div>
            </div>
          )
        })}
      </div>
    </main>
  )
}
