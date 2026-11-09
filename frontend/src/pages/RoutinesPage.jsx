import { useEffect, useMemo, useState } from 'react'
import DOMPurify from 'dompurify'
import { getFieldSessions, getRoutines, markRoutineDone, undoRoutineDone } from '../api/client'
import { trainingDayIso, formatDateMedium } from '../dateUtils'

// The routines' Notes field is plain text with blank-line paragraphs and "- "
// bullets — the two things the program actually uses. Rendered here rather than
// pulling in a markdown dependency for two seeded records.
function RoutineNotes({ text }) {
  const blocks = text.split('\n\n').filter(Boolean)

  return (
    <div className="routine-notes">
      {blocks.map((block, i) => {
        const lines = block.split('\n')
        const bullets = lines.filter((l) => l.startsWith('- '))

        if (bullets.length === lines.length) {
          return (
            <ul key={i}>
              {bullets.map((b, j) => <li key={j}>{b.slice(2)}</li>)}
            </ul>
          )
        }

        // A lead-in line followed by bullets, e.g. "Introduce it over 3 weeks:".
        if (bullets.length > 0) {
          return (
            <div key={i}>
              <p>{lines.filter((l) => !l.startsWith('- ')).join(' ')}</p>
              <ul>
                {bullets.map((b, j) => <li key={j}>{b.slice(2)}</li>)}
              </ul>
            </div>
          )
        }

        return <p key={i}>{block}</p>
      })}
    </div>
  )
}

function RoutineCard({ routine, onChange }) {
  const [open, setOpen] = useState(false)
  const [note, setNote] = useState(routine.today?.notes ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)

  const done = routine.today !== null && routine.today !== undefined
  const lastDone = routine.recent[0]

  async function toggle() {
    setBusy(true)
    setError(null)
    try {
      if (done) {
        await undoRoutineDone(routine.id, routine.today.date)
      } else {
        await markRoutineDone(routine.id, { date: trainingDayIso(), notes: note || null })
      }
      await onChange()
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  async function saveNote() {
    if (!done || note === (routine.today.notes ?? '')) return
    setBusy(true)
    setError(null)
    try {
      await markRoutineDone(routine.id, { date: routine.today.date, notes: note })
      await onChange()
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className={`routine-card${done ? ' routine-card-done' : ''}`}>
      <header className="routine-head">
        <div className="routine-title">
          <h2>{routine.name}</h2>
          <p className="routine-purpose">{routine.purpose}</p>
          <p className="routine-cadence">{routine.cadence}</p>
        </div>
        <button
          type="button"
          className={`routine-tick${done ? ' routine-tick-done' : ''}`}
          onClick={toggle}
          disabled={busy}
          aria-pressed={done}
        >
          {done ? 'Done today' : 'Mark done'}
        </button>
      </header>

      {error && <p className="error">{error}</p>}

      {!done && lastDone && (
        <p className="routine-last">Last done {formatDateMedium(lastDone.date)}</p>
      )}

      <table className="routine-items">
        <tbody>
          {routine.items.map((item) => (
            <tr key={item.id}>
              <th scope="row">{item.name}</th>
              <td className="routine-dose">{item.prescription}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {routine.items.some((i) => i.cue) && (
        <ul className="routine-cues">
          {routine.items.filter((i) => i.cue).map((i) => (
            <li key={i.id}><strong>{i.name}:</strong> {i.cue}</li>
          ))}
        </ul>
      )}

      {done && (
        <label className="routine-note-field">
          <span>Notes</span>
          <input
            type="text"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            onBlur={saveNote}
            placeholder="How did it feel? Anything worth remembering."
            disabled={busy}
          />
        </label>
      )}

      <button type="button" className="routine-toggle" onClick={() => setOpen(!open)}>
        {open ? 'Hide the detail' : 'How to do it'}
      </button>

      {open && <RoutineNotes text={routine.notes} />}
    </section>
  )
}

// Field 1 and Field 2 as the program doc writes them, collapsed so the ankle circuit
// stays at the top of the page. Read-only on purpose: a field session is recorded on
// the watch as its own activity, so there is nothing to tick here.
function FieldSessions({ sessions }) {
  const clean = useMemo(
    () => sessions.map((s) => ({ ...s, safe: s.html ? DOMPurify.sanitize(s.html) : null })),
    [sessions],
  )

  return (
    <section className="field-sessions" aria-label="Field sessions">
      <h2 className="field-sessions-title">Field sessions</h2>
      {clean.map((s) => (
        <details key={s.key} className="field-session">
          <summary>{s.title ?? (s.key === 'field-1' ? 'Field 1' : 'Field 2')}</summary>
          {s.safe
            ? <div className="program-doc" dangerouslySetInnerHTML={{ __html: s.safe }} />
            : <p className="error">Couldn&apos;t find this section in the program document. It may have been retitled.</p>}
        </details>
      ))}
    </section>
  )
}

export default function RoutinesPage() {
  const [routines, setRoutines] = useState(null)
  const [fieldSessions, setFieldSessions] = useState(null)
  const [error, setError] = useState(null)

  async function load() {
    try {
      setRoutines(await getRoutines())
    } catch (err) {
      setError(err.message)
    }
  }

  useEffect(() => { load() }, [])

  // Separate from the routines: no program doc configured (or a down request) must not
  // take the ankle circuit away. Failure just means no field section.
  useEffect(() => {
    getFieldSessions().then((res) => setFieldSessions(res.sessions)).catch(() => setFieldSessions([]))
  }, [])

  return (
    <main className="page">
      <h1>Routines</h1>
      <p className="page-lead">
        The parts of the program that aren&apos;t gym sessions: what to do, and for the ankle
        circuit whether you did it. The field plans are below.
      </p>

      {error && <p className="error">{error}</p>}
      {!error && routines === null && <p>Loading routines…</p>}

      {routines?.map((r) => (
        <RoutineCard key={r.id} routine={r} onChange={load} />
      ))}

      {fieldSessions && fieldSessions.length > 0 && <FieldSessions sessions={fieldSessions} />}
    </main>
  )
}
