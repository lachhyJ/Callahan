import { useCallback, useEffect, useState } from 'react'
import { getWeekPlan, updatePlanSlot, markRoutineDone, undoRoutineDone } from '../api/client'
import { isoDate, startOfWeek, trainingDayIso, formatDateMedium } from '../dateUtils'

const DAY_INITIALS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']

// The rules the plan is arranged around, stated rather than computed. The app
// doesn't know Lachlan's shifts, so it can't tell him which session to drop —
// but it can make sure he isn't deciding from memory.
const RULES = [
  'Gym 1 → 2 → 3 is week order, not priority. The spacing is what keeps a heavy leg day off the front of a field session.',
  'Priority order is Gym 1 → Gym 3 → Gym 2. In a short week, drop Gym 2 first.',
  'Never heavy legs before a speed session — same day or the day before.',
  'Speed and jump work goes first in the day, always.',
  'Field 1 is protected. Speed is use-it-or-lose-it.',
  'Doubling up needs 6 hours between sessions, and two full rest days in the week.',
]

const STATE_LABEL = {
  Done: 'Done',
  Missed: 'Missed',
  Upcoming: '',
  Skipped: 'Skipped',
  Rest: '',
}

function SlotRow({ slot, weekStart, dayOfWeek, onChange }) {
  const [busy, setBusy] = useState(false)
  // Controls stay closed by default. The common use of this page is a glance at
  // what's outstanding; rearranging is a Sunday job. Showing a day picker and
  // two buttons on every slot put 24 controls on the screen and buried the one
  // thing the page is for.
  const [open, setOpen] = useState(false)

  async function update(patch) {
    setBusy(true)
    try {
      await updatePlanSlot(slot.slotId, { weekStart, ...patch })
      await onChange()
    } finally {
      setBusy(false)
    }
  }

  if (slot.kind === 'Rest') {
    return <div className="plan-slot plan-slot-rest">{slot.label}</div>
  }

  const state = slot.state

  return (
    <div className={`plan-slot plan-slot-${state.toLowerCase()}`}>
      <button
        type="button"
        className="plan-slot-main"
        aria-expanded={open}
        onClick={() => setOpen(!open)}
      >
        <span className="plan-slot-label">
          {slot.label}
          {slot.isMoved && <span className="plan-slot-moved">moved</span>}
        </span>
        {STATE_LABEL[state] && (
          <span className="plan-slot-state">
            {STATE_LABEL[state]}
            {state === 'Done' && slot.isManual && ' (by hand)'}
          </span>
        )}
      </button>

      <div className="plan-slot-actions" hidden={!open}>
        <select
          aria-label={`Move ${slot.label} to another day`}
          value={dayOfWeek}
          disabled={busy}
          onChange={(e) => update({ dayOfWeek: Number(e.target.value), status: statusOf(slot) })}
        >
          {DAY_INITIALS.map((d, i) => (
            <option key={i} value={i}>{['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][i]}</option>
          ))}
        </select>

        <button
          type="button"
          disabled={busy}
          onClick={() => update({
            dayOfWeek,
            status: state === 'Done' && slot.isManual ? 'Auto' : 'Done',
          })}
        >
          {state === 'Done' && slot.isManual ? 'Untick' : 'Tick'}
        </button>

        <button
          type="button"
          disabled={busy}
          onClick={() => update({ dayOfWeek, status: state === 'Skipped' ? 'Auto' : 'Skipped' })}
        >
          {state === 'Skipped' ? 'Unskip' : 'Skip'}
        </button>
      </div>
    </div>
  )
}

function statusOf(slot) {
  if (slot.state === 'Skipped') return 'Skipped'
  if (slot.state === 'Done' && slot.isManual) return 'Done'
  return 'Auto'
}

function AnkleStrip({ ankle, days, onChange }) {
  const [busy, setBusy] = useState(false)
  const done = new Set(ankle.completedDates)

  async function toggle(date) {
    setBusy(true)
    try {
      if (done.has(date)) await undoRoutineDone(ankle.routineId, date)
      else await markRoutineDone(ankle.routineId, { date })
      await onChange()
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="ankle-strip">
      <div className="ankle-strip-head">
        <h2>Daily ankle circuit</h2>
        <span>{done.size}/7 this week</span>
      </div>
      <div className="ankle-strip-days">
        {days.map((d, i) => (
          <button
            key={d.date}
            type="button"
            disabled={busy}
            className={`ankle-day${done.has(d.date) ? ' ankle-day-done' : ''}`}
            aria-label={`${d.dayName} ankle circuit`}
            aria-pressed={done.has(d.date)}
            onClick={() => toggle(d.date)}
          >
            {DAY_INITIALS[i]}
          </button>
        ))}
      </div>
    </section>
  )
}

export default function PlanPage() {
  const [weekStart, setWeekStart] = useState(() => isoDate(startOfWeek(new Date())))
  const [plan, setPlan] = useState(null)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    try {
      setPlan(await getWeekPlan(weekStart))
    } catch (err) {
      setError(err.message)
    }
  }, [weekStart])

  useEffect(() => { load() }, [load])

  function shiftWeek(deltaDays) {
    const d = new Date(`${weekStart}T00:00:00`)
    d.setDate(d.getDate() + deltaDays)
    setPlan(null)
    setWeekStart(isoDate(d))
  }

  const thisWeek = isoDate(startOfWeek(new Date()))
  const today = trainingDayIso()

  return (
    <main className="page">
      <h1>This week</h1>

      <div className="plan-weeknav">
        <button type="button" onClick={() => shiftWeek(-7)}>← Previous</button>
        <span>
          {weekStart === thisWeek ? 'This week' : formatDateMedium(weekStart)}
        </span>
        <button type="button" onClick={() => shiftWeek(7)}>Next →</button>
      </div>

      {error && <p className="error">{error}</p>}
      {!error && plan === null && <p>Loading the week…</p>}

      {plan && (
        <>
          {plan.warnings.length > 0 && (
            <section className="plan-warnings">
              {plan.warnings.map((w, i) => <p key={i}>{w}</p>)}
            </section>
          )}

          {plan.ankleCircuit && (
            <AnkleStrip ankle={plan.ankleCircuit} days={plan.days} onChange={load} />
          )}

          <div className="plan-days">
            {plan.days.map((day) => (
              <section
                key={day.date}
                className={`plan-day${day.date === today ? ' plan-day-today' : ''}`}
              >
                <header>
                  <span className="plan-day-name">{day.dayName}</span>
                  <span className="plan-day-date">{formatDateMedium(day.date)}</span>
                </header>
                {day.slots.length === 0
                  ? <p className="plan-day-empty">Nothing planned</p>
                  : day.slots.map((slot) => (
                      <SlotRow
                        key={slot.slotId}
                        slot={slot}
                        weekStart={plan.weekStart}
                        dayOfWeek={day.dayOfWeek}
                        onChange={load}
                      />
                    ))}
              </section>
            ))}
          </div>

          <section className="plan-rules">
            <h2>The rules this is arranged around</h2>
            <ul>{RULES.map((r, i) => <li key={i}>{r}</li>)}</ul>
            <p className="plan-rules-note">
              The app doesn&apos;t know your shifts, so it won&apos;t tell you which session to
              drop. It will tell you when the week you&apos;ve laid out breaks one of these.
            </p>
          </section>
        </>
      )}
    </main>
  )
}
