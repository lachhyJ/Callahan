import { useEffect, useState } from 'react'
import { createTaperEvent, deleteTaperEvent, getTaperEvents, getTaperRecommendation } from '../api/client'

const PHASE_LABELS = {
  build: 'Build',
  early_taper: 'Early taper',
  peak_taper: 'Peak taper',
  sharpen: 'Sharpen',
  game_day: 'Game day',
}

function formatVolume(v) {
  return v === null || v === undefined ? '—' : `${Math.round(v).toLocaleString()}kg`
}

function formatDistance(v) {
  return v === null || v === undefined ? '—' : `${Number(v).toFixed(1)}km`
}

// Fill scaled to actual-vs-baseline, capped so an over-baseline week doesn't
// blow the bar out of its track — mirrors MuscleBalancePage's barScale.
function barScale(actual, baseline) {
  if (!baseline || baseline <= 0) return 0.02
  return Math.min(Math.max(actual / baseline, 0.02), 1)
}

export default function TaperPage() {
  const [recommendation, setRecommendation] = useState(null)
  const [events, setEvents] = useState(null)
  const [error, setError] = useState(null)

  const [date, setDate] = useState('')
  const [name, setName] = useState('')
  const [taperDays, setTaperDays] = useState(10)
  const [saving, setSaving] = useState(false)

  function refresh() {
    getTaperRecommendation().then(setRecommendation).catch((err) => setError(err.message))
    getTaperEvents().then(setEvents).catch((err) => setError(err.message))
  }

  useEffect(refresh, [])

  async function handleCreate(e) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    try {
      await createTaperEvent({ date, name, taperDays: Number(taperDays) || 10 })
      setDate('')
      setName('')
      setTaperDays(10)
      refresh()
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(id, label) {
    if (!window.confirm(`Delete ${label}?`)) return
    try {
      await deleteTaperEvent(id)
      refresh()
    } catch (err) {
      setError(err.message)
    }
  }

  const upcoming = recommendation?.upcomingEvent
  const hasTargets = recommendation && recommendation.phase !== 'none' && recommendation.phase !== 'build'

  return (
    <main className="page page-narrow">
      <h1>Tapering</h1>

      {error && <p className="error">{error}</p>}

      {recommendation === null && <p>Loading…</p>}

      {recommendation && (
        <div className="streak-card section-gap" style={{ flexDirection: 'column', alignItems: 'stretch' }}>
          {upcoming ? (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                <span className="streak-label">{upcoming.name || 'Tournament'}</span>
                <span className="page-subtitle">{upcoming.date}</span>
              </div>
              <p className="streak-value" style={{ fontSize: 'var(--text-lg)' }}>
                {PHASE_LABELS[recommendation.phase] ?? recommendation.phase}
              </p>
              <p className="page-subtitle">{recommendation.message}</p>
            </>
          ) : (
            <p className="page-subtitle">No upcoming tournament set — add one below.</p>
          )}
        </div>
      )}

      {hasTargets && (
        <div className="muscle-bar-list section-gap">
          <div className="muscle-bar-row">
            <span className="muscle-bar-label">Gym</span>
            <div className="muscle-bar-track">
              <div className="muscle-bar-fill" style={{ transform: `scaleX(${barScale(recommendation.gymThisWeekVolume, recommendation.gymBaselineVolume)})` }} />
            </div>
            <span className="muscle-bar-value">{formatVolume(recommendation.gymThisWeekVolume)}</span>
          </div>
          <div className="muscle-bar-row">
            <span className="muscle-bar-label">Running</span>
            <div className="muscle-bar-track">
              <div className="muscle-bar-fill" style={{ transform: `scaleX(${barScale(recommendation.runThisWeekDistanceKm, recommendation.runBaselineDistanceKm)})` }} />
            </div>
            <span className="muscle-bar-value">{formatDistance(recommendation.runThisWeekDistanceKm)}</span>
          </div>
          <p className="page-subtitle">
            Target: about {Math.round((recommendation.gymTargetPct ?? 0) * 100)}% of your recent weekly average
            (gym ~{formatVolume(recommendation.gymBaselineVolume)}, running ~{formatDistance(recommendation.runBaselineDistanceKm)}).
            General taper guidance, not personalized coaching.
          </p>
        </div>
      )}

      <h2 className="section-gap">Add a tournament</h2>
      <form onSubmit={handleCreate}>
        <label>
          Date
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
        </label>
        <label>
          Name (optional)
          <input type="text" placeholder="e.g. Regionals" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label>
          Taper length (days)
          <input type="number" min="1" max="21" value={taperDays} onChange={(e) => setTaperDays(e.target.value)} />
        </label>
        <button type="submit" disabled={saving || !date}>{saving ? 'Saving…' : 'Add tournament'}</button>
      </form>

      {events && events.length > 0 && (
        <div className="section-gap">
          <h2>Tournaments</h2>
          <div className="template-list section-gap">
            {events.map((ev) => (
              <div key={ev.id} className="streak-card">
                <div>
                  <span className="streak-label">{ev.name || 'Tournament'}</span>
                  <div className="page-subtitle">
                    {ev.date} · {ev.daysUntil >= 0 ? `${ev.daysUntil} days away` : 'past'} · {ev.taperDays}-day taper
                  </div>
                </div>
                <button type="button" className="secondary-btn" onClick={() => handleDelete(ev.id, ev.name || 'this tournament')}>Delete</button>
              </div>
            ))}
          </div>
        </div>
      )}
    </main>
  )
}
