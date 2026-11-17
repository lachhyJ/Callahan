import { useState } from 'react'
import { createPortal } from 'react-dom'
import {
  attachSeasonTournaments,
  createSeason,
  deleteSeason,
  updateSeason,
} from '../api/client'
import { formatDateRange, isoDate } from '../dateUtils'
import ConfirmSheet from './ConfirmSheet'

const BLANK = () => ({
  name: '',
  startDate: isoDate(new Date()),
  endDate: isoDate(new Date()),
  targetTournamentId: '',
})

// Same rule the backend uses to link tournaments to a season: the tournament's
// whole date range sits inside the season's. ISO date strings compare correctly.
function inSeasonRange(t, { startDate, endDate }) {
  return t.startDate >= startDate && t.endDate <= endDate
}

function SeasonForm({ initial, tournaments, submitLabel, onSubmit }) {
  const [form, setForm] = useState(initial)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState(null)

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setSaving(true)
    try {
      await onSubmit({
        name: form.name,
        startDate: form.startDate,
        endDate: form.endDate,
        targetTournamentId: form.targetTournamentId === '' ? null : Number(form.targetTournamentId),
      })
    } catch (err) {
      setFormError(err.message)
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="section-gap">
      <label>
        Name
        <input
          type="text"
          value={form.name}
          onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          placeholder="e.g. 2026 Season"
          required
        />
      </label>
      <label>
        Start date
        <input
          type="date"
          value={form.startDate}
          onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))}
          required
        />
      </label>
      <label>
        End date
        <input
          type="date"
          value={form.endDate}
          onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))}
          required
        />
      </label>
      <label>
        Nationals (target tournament)
        <select
          value={form.targetTournamentId}
          onChange={(e) => setForm((f) => ({ ...f, targetTournamentId: e.target.value }))}
        >
          <option value="">— none yet —</option>
          {tournaments
            .filter((t) => inSeasonRange(t, form) || String(t.id) === String(form.targetTournamentId))
            .map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
        </select>
      </label>
      {formError && <p className="error">{formError}</p>}
      <button type="submit" disabled={saving}>{saving ? 'Saving…' : submitLabel}</button>
    </form>
  )
}

// One season's admin, folded to a single line: the Edit / Link tournaments /
// Delete buttons are rarely needed, so they sit behind a tap instead of
// taking a card's worth of space above the games every visit.
// label: optional lead-in ("Current season") shown before the name; when
// showTitle is false (inside an expanded past-season group, whose header
// already names it) only the Nationals line and the toggle show.
export function SeasonAdmin({ season, tournaments, onChanged, label = null, showTitle = true }) {
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState(false)
  const [notice, setNotice] = useState(null)
  const [error, setError] = useState(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  async function handleAttach() {
    setError(null)
    try {
      const { attached } = await attachSeasonTournaments(season.id)
      setNotice(`Linked ${attached} tournament${attached === 1 ? '' : 's'}.`)
      onChanged()
    } catch (err) {
      setError(err.message)
    }
  }

  async function handleDelete() {
    setConfirmingDelete(false)
    try {
      await deleteSeason(season.id)
      onChanged()
    } catch (err) {
      setError(err.message)
    }
  }

  const nationals = season.targetTournamentName ? `Nationals: ${season.targetTournamentName}` : 'No Nationals set'

  return (
    <div className="season-admin">
      <button type="button" className="season-admin-toggle" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <span className="season-admin-text">
          {showTitle && (
            <span className="season-admin-name">
              {label && <span className="season-admin-label">{label} · </span>}
              {season.name}
            </span>
          )}
          <span className="season-admin-meta">
            {showTitle ? `${formatDateRange(season.startDate, season.endDate)} · ` : 'Season options · '}{nationals}
          </span>
        </span>
        <span className="season-admin-chevron" aria-hidden="true">{open ? '▾' : '▸'}</span>
      </button>

      {open && !editing && (
        <div className="games-header-actions">
          <button type="button" className="secondary-btn" onClick={() => { setNotice(null); setEditing(true) }}>Edit</button>
          <button type="button" className="secondary-btn" onClick={handleAttach}>Link tournaments</button>
          <button type="button" className="secondary-btn" onClick={() => setConfirmingDelete(true)}>Delete</button>
        </div>
      )}

      {editing && (
        <>
          <SeasonForm
            initial={{
              name: season.name,
              startDate: season.startDate,
              endDate: season.endDate,
              targetTournamentId: season.targetTournamentId ?? '',
            }}
            tournaments={tournaments}
            submitLabel="Save changes"
            onSubmit={async (payload) => {
              await updateSeason(season.id, payload)
              setEditing(false)
              onChanged()
            }}
          />
          <button type="button" className="secondary-btn section-gap" onClick={() => setEditing(false)}>Cancel</button>
        </>
      )}

      {notice && <p className="save-confirm">✓ {notice}</p>}
      {error && <p className="error">{error}</p>}

      <ConfirmSheet
        open={confirmingDelete}
        variant="danger"
        title={`Delete ${season.name}?`}
        body="Its tournaments stay, just unlinked. This cannot be undone."
        confirmLabel="Delete season"
        onConfirm={handleDelete}
        onCancel={() => setConfirmingDelete(false)}
      />
    </div>
  )
}

// Top-of-page season area on the Ultimate page: the "+ Season" button and its
// create form, plus a folded admin line for each season that isn't past
// (the current one, and any already entered for later). Past seasons get
// their admin line inside their own group further down the page.
// actionSlot: optional DOM node (in the page header) the create/cancel button is
// portalled into, so it sits on one line with the page's other header actions.
export default function SeasonsSection({ seasons, topSeasons, currentId, tournaments, onChanged, actionSlot = null }) {
  const [creating, setCreating] = useState(false)

  const seasonButton = (
    <button type="button" className="secondary-btn btn-compact" onClick={() => setCreating((c) => !c)}>
      {creating ? 'Cancel' : '+ Season'}
    </button>
  )

  return (
    <section className="section-gap">
      {actionSlot ? createPortal(seasonButton, actionSlot) : (
        <div className="history-week-header">
          <h2 className="trend-chart-title">Seasons</h2>
          {seasonButton}
        </div>
      )}

      {creating && (
        <SeasonForm
          initial={BLANK()}
          tournaments={tournaments}
          submitLabel="Create season"
          onSubmit={async (payload) => {
            await createSeason(payload)
            setCreating(false)
            onChanged()
          }}
        />
      )}

      {seasons.length === 0 && !creating && (
        <p className="trend-chart-caption">No seasons yet — add one to shade it on the Trends strength chart.</p>
      )}

      {topSeasons.map((s) => (
        <SeasonAdmin
          key={s.id}
          season={s}
          tournaments={tournaments}
          onChanged={onChanged}
          label={s.id === currentId ? (s.endDate < isoDate(new Date()) ? 'Last season' : 'Current season') : 'Next season'}
        />
      ))}
    </section>
  )
}
