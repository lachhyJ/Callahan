import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import {
  attachSeasonTournaments,
  createSeason,
  deleteSeason,
  getSeasons,
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

// Season admin: create / edit / delete a season and pick its target
// ("Nationals") tournament. Lives on the Ultimate page above the tournament
// list. `tournaments` is passed in for the target picker.
// actionSlot: optional DOM node (in the page header) the create/cancel button is
// portalled into, so it sits on one line with the page's other header actions.
export default function SeasonsSection({ tournaments, actionSlot = null }) {
  const [seasons, setSeasons] = useState(null)
  const [error, setError] = useState(null)

  const [open, setOpen] = useState(false)
  const [editingId, setEditingId] = useState(null)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState(null)
  const [notice, setNotice] = useState(null)
  const [confirmingDeleteId, setConfirmingDeleteId] = useState(null)

  function load() {
    getSeasons().then(setSeasons).catch((err) => setError(err.message))
  }

  useEffect(load, [])

  function startCreate() {
    setEditingId(null)
    setForm(BLANK())
    setFormError(null)
    setOpen(true)
  }

  function startEdit(s) {
    setEditingId(s.id)
    setForm({
      name: s.name,
      startDate: s.startDate,
      endDate: s.endDate,
      targetTournamentId: s.targetTournamentId ?? '',
    })
    setFormError(null)
    setOpen(true)
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setSaving(true)
    const payload = {
      name: form.name,
      startDate: form.startDate,
      endDate: form.endDate,
      targetTournamentId: form.targetTournamentId === '' ? null : Number(form.targetTournamentId),
    }
    try {
      if (editingId == null) await createSeason(payload)
      else await updateSeason(editingId, payload)
      setOpen(false)
      setNotice(null)
      load()
    } catch (err) {
      setFormError(err.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(id) {
    setConfirmingDeleteId(null)
    try {
      await deleteSeason(id)
      load()
    } catch (err) {
      setError(err.message)
    }
  }

  async function handleAttach(id) {
    try {
      const { attached } = await attachSeasonTournaments(id)
      setNotice(`Linked ${attached} tournament${attached === 1 ? '' : 's'}.`)
      load()
    } catch (err) {
      setError(err.message)
    }
  }

  if (error) return <p className="error section-gap">{error}</p>
  if (!seasons) return null

  const creating = open && editingId == null
  const seasonButton = (
    <button type="button" className="secondary-btn btn-compact" onClick={creating ? () => setOpen(false) : startCreate}>
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

      {open && (
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
          <button type="submit" disabled={saving}>
            {saving ? 'Saving…' : editingId == null ? 'Create season' : 'Save changes'}
          </button>
        </form>
      )}

      {notice && <p className="save-confirm section-gap">✓ {notice}</p>}

      {seasons.length === 0 && !open && (
        <p className="trend-chart-caption">No seasons yet — add one to shade it on the Trends strength chart.</p>
      )}

      <div className="history-week-list section-gap">
        {seasons.map((s) => (
          <div key={s.id} className="history-week">
            <div className="history-week-header">
              <span>{s.name} · {formatDateRange(s.startDate, s.endDate)}</span>
              <span className="history-week-count">{s.tournamentCount} tournament{s.tournamentCount === 1 ? '' : 's'}</span>
            </div>
            <p className="tournament-section-summary">
              {s.targetTournamentName ? `Nationals: ${s.targetTournamentName}` : 'No target tournament set'}
            </p>
            <div className="games-header-actions">
              <button type="button" className="secondary-btn" onClick={() => startEdit(s)}>Edit</button>
              <button type="button" className="secondary-btn" onClick={() => handleAttach(s.id)}>Link tournaments</button>
              <button type="button" className="secondary-btn" onClick={() => setConfirmingDeleteId(s.id)}>Delete</button>
            </div>
          </div>
        ))}
      </div>
      <ConfirmSheet
        open={confirmingDeleteId != null}
        variant="danger"
        title="Delete this season?"
        body="Its tournaments stay, just unlinked. This cannot be undone."
        confirmLabel="Delete season"
        onConfirm={() => handleDelete(confirmingDeleteId)}
        onCancel={() => setConfirmingDeleteId(null)}
      />
    </section>
  )
}
