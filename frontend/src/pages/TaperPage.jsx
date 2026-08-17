import { useEffect, useState } from 'react'
import {
  createTaperEvent,
  deleteTaperEvent,
  getTaperCheckIns,
  getTaperConsult,
  getTaperEvents,
  getTaperRecommendation,
  upsertTaperCheckIn,
} from '../api/client'
import { enablePushNotifications, hasActiveSubscription, pushSupported } from '../push'
import { isoDate } from '../dateUtils'

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

// Pure UTC arithmetic, deliberately — parsing as local midnight and
// formatting via toISOString (always UTC) cancels a +1-day step to zero
// in any timezone ahead of UTC, which made the check-in window loop below
// spin forever pushing the same date. UTC-only sidesteps that entirely.
function addDays(isoDate, days) {
  const [y, m, d] = isoDate.split('-').map(Number)
  const date = new Date(Date.UTC(y, m - 1, d))
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

function todayIso() {
  return isoDate(new Date())
}

// Full expected check-in window (taper start through debrief day+3), built
// client-side so gap days can be shown even though the backend only returns
// entries that actually exist — a missing day is meaningful, not just absent.
function buildWindow(eventDate, taperDays) {
  const start = addDays(eventDate, -taperDays)
  const end = addDays(eventDate, 3)
  const dates = []
  for (let d = start; d <= end; d = addDays(d, 1)) {
    dates.push(d)
  }
  return dates
}

const TABS = [
  { id: 'today', label: 'Today' },
  { id: 'history', label: 'History' },
  { id: 'ask', label: 'Ask AI' },
  { id: 'tournaments', label: 'Tournaments' },
]

// Minimal, dependency-free rendering for the AI consult's answer — it comes
// back as loose markdown (paragraphs, **bold**), not HTML, and the model
// isn't asked to avoid that formatting since it reads naturally in a chat
// context. No need for a full markdown library for two constructs.
function ConsultAnswer({ text }) {
  return (
    <>
      {text.split(/\n{2,}/).map((paragraph, i) => (
        <p key={i}>
          {paragraph.split('\n').map((line, j, lines) => (
            <span key={j}>
              {line.split(/(\*\*[^*]+\*\*)/g).map((part, k) =>
                part.startsWith('**') && part.endsWith('**')
                  ? <strong key={k}>{part.slice(2, -2)}</strong>
                  : part
              )}
              {j < lines.length - 1 && <br />}
            </span>
          ))}
        </p>
      ))}
    </>
  )
}

function RatingInput({ label, value, onChange }) {
  return (
    <label>
      {label}
      <div className="rating-row">
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            key={n}
            type="button"
            className={`rating-btn${value === n ? ' active' : ''}`}
            onClick={() => onChange(n)}
          >
            {n}
          </button>
        ))}
      </div>
    </label>
  )
}

export default function TaperPage() {
  const [recommendation, setRecommendation] = useState(null)
  const [events, setEvents] = useState(null)
  const [error, setError] = useState(null)

  const [date, setDate] = useState('')
  const [name, setName] = useState('')
  const [taperDays, setTaperDays] = useState(10)
  const [saving, setSaving] = useState(false)

  const [checkIns, setCheckIns] = useState(null)
  const [checkInDate, setCheckInDate] = useState(todayIso())
  const [energy, setEnergy] = useState(null)
  const [soreness, setSoreness] = useState(null)
  const [motivation, setMotivation] = useState(null)
  const [context, setContext] = useState('')
  const [checkInSaving, setCheckInSaving] = useState(false)
  const [checkInError, setCheckInError] = useState(null)

  const [question, setQuestion] = useState('')
  const [consultAnswer, setConsultAnswer] = useState(null)
  const [consultCompared, setConsultCompared] = useState(false)
  const [consulting, setConsulting] = useState(false)
  const [consultError, setConsultError] = useState(null)

  const [pushEnabled, setPushEnabled] = useState(true)
  const [pushError, setPushError] = useState(null)

  const [tab, setTab] = useState('today')

  function refresh() {
    getTaperRecommendation().then(setRecommendation).catch((err) => setError(err.message))
    getTaperEvents().then(setEvents).catch((err) => setError(err.message))
  }

  useEffect(refresh, [])
  useEffect(() => {
    hasActiveSubscription().then(setPushEnabled).catch(() => {})
  }, [])

  const upcoming = recommendation?.upcomingEvent
  const hasTargets = recommendation && recommendation.phase !== 'none' && recommendation.phase !== 'build'

  const upcomingId = upcoming?.id
  useEffect(() => {
    if (upcomingId) {
      getTaperCheckIns(upcomingId).then(setCheckIns).catch((err) => setError(err.message))
      // Re-sync to today whenever the active tournament changes — a stale
      // date from a previous tournament's window can fall outside the new
      // one's, since each has its own taper length.
      setCheckInDate(todayIso())
    } else {
      setCheckIns(null)
    }
  }, [upcomingId])

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

  async function handleCheckInSubmit(e) {
    e.preventDefault()
    if (!upcoming || !energy || !soreness || !motivation) return
    setCheckInError(null)
    setCheckInSaving(true)
    try {
      await upsertTaperCheckIn(upcoming.id, { date: checkInDate, energy, soreness, motivation, context })
      const updated = await getTaperCheckIns(upcoming.id)
      setCheckIns(updated)
      setEnergy(null)
      setSoreness(null)
      setMotivation(null)
      setContext('')
    } catch (err) {
      setCheckInError(err.message)
    } finally {
      setCheckInSaving(false)
    }
  }

  async function handleConsult(e) {
    e.preventDefault()
    if (!upcoming) return
    // Otherwise the question textarea keeps its focus ring while the page
    // scrolls to the answer once it lands — reads as a stray highlight.
    e.target.querySelector('textarea')?.blur()
    setConsultError(null)
    setConsultAnswer(null)
    setConsulting(true)
    try {
      const result = await getTaperConsult(upcoming.id, question)
      setConsultAnswer(result.answer)
      setConsultCompared(result.comparedToPriorTaper)
    } catch (err) {
      setConsultError(err.message)
    } finally {
      setConsulting(false)
    }
  }

  async function handleEnablePush() {
    setPushError(null)
    try {
      await enablePushNotifications()
      setPushEnabled(true)
    } catch (err) {
      setPushError(err.message)
    }
  }

  const isDebriefDate = upcoming && checkInDate > upcoming.date
  const windowDates = upcoming ? buildWindow(upcoming.date, upcoming.taperDays) : []
  const checkInsByDate = new Map((checkIns ?? []).map((c) => [c.date, c]))

  return (
    <main className="page page-narrow">
      <h1>Tapering</h1>

      {error && <p className="error">{error}</p>}

      {recommendation === null && <p>Loading…</p>}

      {recommendation && !upcoming && (
        <>
          <div className="streak-card section-gap" style={{ flexDirection: 'column', alignItems: 'stretch' }}>
            <p className="page-subtitle">No upcoming tournament set — add one below.</p>
          </div>
          <TournamentsTab
            date={date} setDate={setDate} name={name} setName={setName}
            taperDays={taperDays} setTaperDays={setTaperDays} saving={saving}
            handleCreate={handleCreate} events={events} handleDelete={handleDelete}
          />
        </>
      )}

      {recommendation && upcoming && (
        <>
          <nav className="taper-tabs section-gap">
            {TABS.map((t) => (
              <button
                key={t.id}
                type="button"
                className={`taper-tab${tab === t.id ? ' active' : ''}`}
                onClick={() => setTab(t.id)}
              >
                {t.label}
              </button>
            ))}
          </nav>

          {tab === 'today' && (
            <>
              <div className="streak-card section-gap" style={{ flexDirection: 'column', alignItems: 'stretch' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                  <span className="streak-label">{upcoming.name || 'Tournament'}</span>
                  <span className="page-subtitle">{upcoming.date}</span>
                </div>
                <p className="streak-value" style={{ fontSize: 'var(--text-lg)' }}>
                  {PHASE_LABELS[recommendation.phase] ?? recommendation.phase}
                </p>
                <p className="page-subtitle">{recommendation.message}</p>
              </div>

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

              {hasTargets ? (
                <>
                  <h2 className="section-gap">{isDebriefDate ? 'Debrief' : 'Daily check-in'}</h2>
                  {!pushEnabled && pushSupported() && (
                    <div className="push-prompt">
                      <span>Get an evening reminder if you miss a check-in</span>
                      <button type="button" className="secondary-btn" onClick={handleEnablePush}>Enable notifications</button>
                      {pushError && <p className="error">{pushError}</p>}
                    </div>
                  )}
                  <form onSubmit={handleCheckInSubmit} className="section-gap">
                    <label>
                      Date
                      <input
                        type="date"
                        value={checkInDate}
                        min={addDays(upcoming.date, -upcoming.taperDays)}
                        max={addDays(upcoming.date, 3)}
                        onChange={(e) => setCheckInDate(e.target.value)}
                      />
                    </label>
                    <RatingInput label="Energy" value={energy} onChange={setEnergy} />
                    <RatingInput label="Soreness" value={soreness} onChange={setSoreness} />
                    <RatingInput label="Motivation" value={motivation} onChange={setMotivation} />
                    <label>
                      {isDebriefDate ? "How'd it go? What would you change?" : 'Anything unusual today? (bad sleep, hard work day, travel...)'}
                      <textarea value={context} onChange={(e) => setContext(e.target.value)} />
                    </label>
                    {checkInError && <p className="error">{checkInError}</p>}
                    <button type="submit" disabled={checkInSaving || !energy || !soreness || !motivation}>
                      {checkInSaving ? 'Saving…' : 'Save check-in'}
                    </button>
                  </form>
                </>
              ) : (
                <p className="page-subtitle section-gap">Check-ins open once the taper window starts, {upcoming.taperDays} days before {upcoming.date}.</p>
              )}
            </>
          )}

          {tab === 'history' && (
            <div className="section-gap">
              {checkIns === null && <p>Loading…</p>}
              {checkIns !== null && windowDates.every((d) => !checkInsByDate.get(d) && d >= todayIso()) && (
                <div className="empty-state">
                  <p>No check-ins yet — they'll show up here once you log one from the Today tab.</p>
                </div>
              )}
              {checkIns !== null && windowDates.map((d) => {
                const entry = checkInsByDate.get(d)
                const isPast = d < todayIso()
                if (!entry && !isPast) return null
                return entry ? (
                  <div key={d} className="checkin-row">
                    <span>{d}{d > upcoming.date ? ' (debrief)' : ''}</span>
                    <span className="page-subtitle">
                      E{entry.energy} S{entry.soreness} M{entry.motivation}
                      {entry.context ? ` — ${entry.context}` : ''}
                    </span>
                  </div>
                ) : (
                  <div key={d} className="checkin-row gap">
                    <span>{d}</span>
                    <span>No check-in logged</span>
                  </div>
                )
              })}
            </div>
          )}

          {tab === 'ask' && (
            <>
              {recommendation.tapersCompleted === 0 && (
                <div className="empty-state section-gap">
                  <p>Complete this taper to unlock comparisons with future ones.</p>
                </div>
              )}
              <form onSubmit={handleConsult} className="section-gap">
                <label>
                  Question (optional — defaults to a general check)
                  <textarea
                    value={question}
                    onChange={(e) => setQuestion(e.target.value)}
                    placeholder="Anything I should know about this taper?"
                  />
                </label>
                <button type="submit" disabled={consulting}>{consulting ? 'Asking…' : 'Ask'}</button>
              </form>
              {consulting && <p className="page-subtitle">Thinking — this can take several seconds…</p>}
              {consultError && <p className="error">{consultError}</p>}
              {consultAnswer && (
                <div className="streak-card section-gap" style={{ flexDirection: 'column', alignItems: 'stretch' }}>
                  <ConsultAnswer text={consultAnswer} />
                  {consultCompared && <p className="page-subtitle">(Compared against your last taper)</p>}
                </div>
              )}
            </>
          )}

          {tab === 'tournaments' && (
            <TournamentsTab
              date={date} setDate={setDate} name={name} setName={setName}
              taperDays={taperDays} setTaperDays={setTaperDays} saving={saving}
              handleCreate={handleCreate} events={events} handleDelete={handleDelete}
            />
          )}
        </>
      )}
    </main>
  )
}

function TournamentsTab({ date, setDate, name, setName, taperDays, setTaperDays, saving, handleCreate, events, handleDelete }) {
  return (
    <>
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
    </>
  )
}
