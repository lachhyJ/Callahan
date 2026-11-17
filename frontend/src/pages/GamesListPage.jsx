import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getActivities,
  getTournaments,
  getSeasons,
  createTournament,
  updateTournament,
  deleteTournament,
  attachTournamentGames,
} from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import { formatHoursMinutes } from '../utils/format'
import { summariseGames, unclassifiedTournamentActivities } from '../utils/tournamentStats'
import GameRow from '../components/GameRow'
import PendingGameRow from '../components/PendingGameRow'
import UndoToast from '../components/UndoToast'
import useMarkAsGame from '../hooks/useMarkAsGame'
import SeasonsSection, { SeasonLink } from '../components/SeasonsSection'
import { RECENT_DAYS, rank, sortEntries, partitionBySeason, groupCounts } from '../utils/ultimateList'
import ConfirmSheet from '../components/ConfirmSheet'
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
  const [unclassified, setUnclassified] = useState([])
  const [tournaments, setTournaments] = useState(null)
  const [seasons, setSeasons] = useState(null)
  const [openPast, setOpenPast] = useState(() => new Set()) // past-season group keys expanded
  const [error, setError] = useState(null)

  const [showForm, setShowForm] = useState(false)
  // null while creating, a tournament id while editing that one.
  const [editingId, setEditingId] = useState(null)
  const [seasonSlot, setSeasonSlot] = useState(null) // header node the seasons button portals into
  const [syncResult, setSyncResult] = useState(null) // { text, isError } from the header sync icon
  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState(isoDate(new Date()))
  const [endDate, setEndDate] = useState(isoDate(new Date()))
  // '' means no taper. Kept as a string so the number input can be emptied.
  const [taperDays, setTaperDays] = useState('')
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState(null)
  const [lastAttached, setLastAttached] = useState(null)
  const [confirmingDelete, setConfirmingDelete] = useState(null)
  const [showAllUpcoming, setShowAllUpcoming] = useState(false)

  const load = useCallback(() => {
    Promise.all([
      getActivities({ type: 'Ultimate', sessionType: 'Game' }),
      getTournaments(),
      getActivities({ type: 'Ultimate' }),
      getSeasons(),
    ])
      .then(([g, t, all, s]) => {
        setGames(g)
        setUnclassified(unclassifiedTournamentActivities(all, g))
        setTournaments(t)
        setSeasons(s)
      })
      .catch((err) => setError(err.message))
  }, [])

  useEffect(load, [load])

  const { markAsGame, busy, error: markError, toast, dismiss } = useMarkAsGame(load)

  useEffect(() => {
    if (!syncResult) return
    const timeout = setTimeout(() => setSyncResult(null), 6000)
    return () => clearTimeout(timeout)
  }, [syncResult])

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

  // Not recoverable (no Recently Deleted for tournaments), and it takes any
  // taper check-ins with it — so the danger sheet, not an OS alert.
  function deleteConsequences(t) {
    const gameCount = t.gameCount ?? 0
    return [
      gameCount > 0 ? `Its ${gameCount} game${gameCount === 1 ? '' : 's'} stay, just ungrouped.` : null,
      t.taperDays != null ? 'Its taper check-ins are deleted.' : null,
      'This cannot be undone.',
    ].filter(Boolean).join(' ')
  }

  async function handleDelete(t) {
    setConfirmingDelete(null)
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

  if (!games || !tournaments || !seasons) {
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
  const today = isoDate(new Date())
  const recentCutoff = new Date()
  recentCutoff.setDate(recentCutoff.getDate() - RECENT_DAYS)
  const recentSince = isoDate(recentCutoff)
  const sorted = sortEntries([
    ...tournaments.map((t) => {
      const gs = byTournament.get(t.id) ?? []
      const pending = unclassified.filter((a) => a.tournamentId === t.id)
      return { kind: 'tournament', tournament: t, games: gs, pending, sortDate: gs[0]?.date ?? pending[0]?.date ?? t.startDate }
    }),
    ...loose.map((g) => ({ kind: 'game', game: g, sortDate: g.date })),
  ], today, recentSince)
  // Older seasons fold into one row each at the bottom, so the page opens on
  // this season's games rather than every season's.
  const { current, main, pastGroups } = partitionBySeason(sorted, seasons, today, recentSince)
  const pastSeasonIds = new Set(pastGroups.filter((g) => g.season).map((g) => g.season.id))
  const topSeasons = seasons.filter((s) => !pastSeasonIds.has(s.id))

  // Only the next upcoming tournament shows in full; the rest fold behind one
  // toggle that takes its place in the list, so pre-entering next season's
  // calendar doesn't push the past weekend's games off the first screen.
  const upcoming = main.filter((e) => rank(e, today, recentSince) === 1)
  const laterUpcoming = upcoming.slice(1)
  const entries = showAllUpcoming || laterUpcoming.length === 0
    ? main
    : main.flatMap((e) => {
        if (laterUpcoming[0] === e) return [{ kind: 'more-upcoming', count: laterUpcoming.length }]
        return laterUpcoming.includes(e) ? [] : [e]
      })

  const tournamentForm = (
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
  )

  function togglePast(key) {
    setOpenPast((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function renderEntry(entry) {
    if (entry.kind === 'more-upcoming') {
      return (
        <button
          key="more-upcoming"
          type="button"
          className="report-more-toggle more-upcoming-toggle"
          onClick={() => setShowAllUpcoming(true)}
        >
          + {entry.count} more upcoming tournament{entry.count === 1 ? '' : 's'}
        </button>
      )
    }
    if (entry.kind === 'game') {
      return (
        <div key={`g-${entry.game.id}`} className="history-week">
          <GameRow game={entry.game} />
        </div>
      )
    }
    const { tournament, games: gs, pending } = entry
    const summaryLine = tournamentSummaryLine(gs)
    if (showForm && editingId === tournament.id) {
      return (
        <div key={`t-${tournament.id}`} className="history-week">
          {tournamentForm}
        </div>
      )
    }
    return (
      <div key={`t-${tournament.id}`} className="history-week">
        <Link to={`/tournaments/${tournament.id}`} className="tournament-section-link">
          <div className="history-week-header">
            <span>{tournament.name} · {formatDateRange(tournament.startDate, tournament.endDate)}</span>
            <span className="history-week-count">
              {gs.length === 0 && pending.length === 0
                ? 'no games yet'
                : [
                    gs.length > 0 && `${gs.length} game${gs.length === 1 ? '' : 's'}`,
                    pending.length > 0 && `${pending.length} to classify`,
                  ].filter(Boolean).join(' · ')}
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
        {pending.map((a) => <PendingGameRow key={a.id} activity={a} onMarkGame={markAsGame} disabled={busy} />)}
        <div className="games-header-actions">
          <button type="button" className="secondary-btn" onClick={() => startEdit(tournament)}>Edit</button>
          <button type="button" className="secondary-btn" onClick={() => setConfirmingDelete(tournament)}>Delete</button>
        </div>
      </div>
    )
  }

  return (
    <main className="page">
      <div className="history-week-header">
        <h1>Ultimate</h1>
        <div className="games-header-actions">
          <SyncGarminButton variant="icon" onSynced={load} onResult={setSyncResult} />
          <button
            type="button"
            className="secondary-btn btn-compact"
            onClick={showForm ? () => { setShowForm(false); setEditingId(null) } : startCreate}
          >
            {showForm ? 'Cancel' : '+ Tournament'}
          </button>
          <span ref={setSeasonSlot} className="games-header-slot" />
        </div>
      </div>

      {syncResult && (
        <p className={syncResult.isError ? 'save-confirmation error' : 'save-confirmation'}>
          {syncResult.text}
        </p>
      )}

      <SeasonsSection
        seasons={seasons}
        topSeasons={topSeasons}
        currentId={current?.id ?? null}
        tournaments={tournaments}
        onChanged={load}
        actionSlot={seasonSlot}
      />

      {showForm && editingId == null && tournamentForm}

      {lastAttached && (
        <p className="save-confirm section-gap">
          ✓ {lastAttached.name}: attached {lastAttached.attached} game{lastAttached.attached === 1 ? '' : 's'}
        </p>
      )}

      {markError && <p className="error section-gap">{markError}</p>}

      {games.length === 0 && (
        <div className="empty-state section-gap">
          <p>No games classified as "Game" yet — classify an Ultimate activity from History to see it here.</p>
        </div>
      )}

      <div className="history-week-list section-gap">
        {entries.map(renderEntry)}
      </div>

      {pastGroups.length > 0 && (
        <section className="section-gap">
          <h2 className="trend-chart-title">Past seasons</h2>
          <div className="history-week-list">
            {pastGroups.map((group) => {
              const key = group.season ? group.season.id : 'none'
              const isOpen = openPast.has(key)
              const { tournaments: tc, games: gc } = groupCounts(group.entries)
              const counts = [
                `${tc} tournament${tc === 1 ? '' : 's'}`,
                `${gc} game${gc === 1 ? '' : 's'}`,
              ].join(' · ')
              return (
                <div key={`past-${key}`} className="past-season">
                  <button
                    type="button"
                    className="past-season-toggle history-week"
                    aria-expanded={isOpen}
                    onClick={() => togglePast(key)}
                  >
                    <span className="history-week-header">
                      <span>{group.season ? `${group.season.name} · ${formatDateRange(group.season.startDate, group.season.endDate)}` : 'Earlier, no season'}</span>
                      <span className="history-week-count">{counts} {isOpen ? '▾' : '▸'}</span>
                    </span>
                  </button>
                  {isOpen && (
                    <div className="history-week-list past-season-body">
                      {group.season && (
                        <SeasonLink season={group.season} showTitle={false} />
                      )}
                      {group.entries.map(renderEntry)}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </section>
      )}
      {toast && <UndoToast message={toast.message} onUndo={toast.undo} onDismiss={dismiss} />}
      <ConfirmSheet
        open={confirmingDelete != null}
        variant="danger"
        title={confirmingDelete ? `Delete ${confirmingDelete.name}?` : ''}
        body={confirmingDelete ? deleteConsequences(confirmingDelete) : ''}
        confirmLabel="Delete tournament"
        onConfirm={() => handleDelete(confirmingDelete)}
        onCancel={() => setConfirmingDelete(null)}
      />
    </main>
  )
}
