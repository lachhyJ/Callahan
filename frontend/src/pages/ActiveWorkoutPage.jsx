import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { createWorkoutSession, startWorkoutTemplate } from '../api/client'

const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }
const SET_TYPE_OPTIONS = ['Warmup', 'Normal', 'Failure', 'Drop']

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function buildInitialSets(targetSets, previousSets) {
  const previousByOrder = new Map(previousSets.map((p) => [p.setOrder, p]))
  return Array.from({ length: targetSets }, (_, i) => {
    const setOrder = i + 1
    const previous = previousByOrder.get(setOrder) ?? null
    return {
      setOrder,
      reps: previous ? String(previous.reps) : '',
      weightKg: previous ? String(previous.weightKg) : '',
      previous,
      completed: false,
      type: 'Normal',
    }
  })
}

export default function ActiveWorkoutPage() {
  const { templateId } = useParams()
  const [templateName, setTemplateName] = useState('')
  const [exercises, setExercises] = useState(null)
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const [openTypeMenu, setOpenTypeMenu] = useState(null)
  const navigate = useNavigate()

  useEffect(() => {
    startWorkoutTemplate(templateId)
      .then((data) => {
        setTemplateName(data.templateName)
        setExercises(
          data.exercises.map((ex) => ({
            exerciseId: ex.exerciseId,
            exerciseName: ex.exerciseName,
            targetReps: ex.targetReps,
            notes: '',
            sets: buildInitialSets(ex.targetSets, ex.previousSets),
          }))
        )
      })
      .catch((err) => setError(err.message))
  }, [templateId])

  function updateSet(exIdx, setIdx, field, value) {
    setExercises((prev) =>
      prev.map((ex, i) =>
        i !== exIdx
          ? ex
          : { ...ex, sets: ex.sets.map((s, j) => (j !== setIdx ? s : { ...s, [field]: value })) }
      )
    )
  }

  function updateNotes(exIdx, value) {
    setExercises((prev) => prev.map((ex, i) => (i !== exIdx ? ex : { ...ex, notes: value })))
  }

  function toggleComplete(exIdx, setIdx) {
    setExercises((prev) =>
      prev.map((ex, i) =>
        i !== exIdx
          ? ex
          : { ...ex, sets: ex.sets.map((s, j) => (j !== setIdx ? s : { ...s, completed: !s.completed })) }
      )
    )
  }

  function setType(exIdx, setIdx, type) {
    updateSet(exIdx, setIdx, 'type', type)
    setOpenTypeMenu(null)
  }

  function removeSet(exIdx, setIdx) {
    setExercises((prev) =>
      prev.map((ex, i) =>
        i !== exIdx
          ? ex
          : { ...ex, sets: ex.sets.filter((_, j) => j !== setIdx).map((s, j) => ({ ...s, setOrder: j + 1 })) }
      )
    )
    setOpenTypeMenu(null)
  }

  function addSet(exIdx) {
    setExercises((prev) =>
      prev.map((ex, i) =>
        i !== exIdx
          ? ex
          : {
              ...ex,
              sets: [
                ...ex.sets,
                { setOrder: ex.sets.length + 1, reps: '', weightKg: '', previous: null, completed: false, type: 'Normal' },
              ],
            }
      )
    )
  }

  async function handleFinish() {
    setError(null)
    setSaving(true)
    try {
      const sets = exercises.flatMap((ex) =>
        ex.sets
          .filter((s) => s.completed && s.reps !== '' && s.weightKg !== '')
          .map((s) => ({
            exerciseId: ex.exerciseId,
            reps: Number(s.reps),
            weightKg: Number(s.weightKg),
            setOrder: s.setOrder,
            setType: s.type,
          }))
      )

      if (sets.length === 0) {
        setError('Tick at least one completed set before finishing.')
        setSaving(false)
        return
      }

      const exerciseNotes = exercises
        .filter((ex) => ex.notes.trim() !== '')
        .map((ex) => ({ exerciseId: ex.exerciseId, notes: ex.notes.trim() }))

      await createWorkoutSession({
        date: todayIso(),
        notes: null,
        workoutTemplateId: Number(templateId),
        sets,
        exerciseNotes,
      })
      navigate('/history')
    } catch (err) {
      setError(err.message)
      setSaving(false)
    }
  }

  if (error && !exercises) return <main className="page"><p className="error">{error}</p></main>
  if (!exercises) return <main className="page"><p>Loading…</p></main>

  return (
    <main className="page">
      <div className="active-workout-header">
        <h1>{templateName}</h1>
        <button type="button" onClick={handleFinish} disabled={saving}>
          {saving ? 'Saving…' : 'Finish'}
        </button>
      </div>
      {error && <p className="error">{error}</p>}

      {exercises.map((ex, exIdx) => (
        <div key={ex.exerciseId} className="exercise-card">
          <h2>{ex.exerciseName}</h2>
          <p className="target-reps">Target: {ex.sets.length} × {ex.targetReps}</p>
          <input
            type="text"
            placeholder="Add notes here…"
            className="exercise-notes-input"
            value={ex.notes}
            onChange={(e) => updateNotes(exIdx, e.target.value)}
          />
          <table>
            <thead>
              <tr>
                <th>Set</th>
                <th>Previous</th>
                <th>Kg</th>
                <th>Reps</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {ex.sets.map((s, setIdx) => (
                <tr key={setIdx} className={s.completed ? 'set-row completed' : 'set-row'}>
                  <td className="set-number-cell">
                    <button
                      type="button"
                      className={`set-number set-type-${s.type.toLowerCase()}`}
                      onClick={() => setOpenTypeMenu(openTypeMenu?.exIdx === exIdx && openTypeMenu?.setIdx === setIdx ? null : { exIdx, setIdx })}
                    >
                      {SET_TYPE_LABELS[s.type] || s.setOrder}
                    </button>
                    {openTypeMenu?.exIdx === exIdx && openTypeMenu?.setIdx === setIdx && (
                      <div className="set-type-menu">
                        {SET_TYPE_OPTIONS.map((opt) => (
                          <button key={opt} type="button" onClick={() => setType(exIdx, setIdx, opt)}>
                            {opt}
                          </button>
                        ))}
                        <button type="button" className="remove-option" onClick={() => removeSet(exIdx, setIdx)}>
                          Remove set
                        </button>
                      </div>
                    )}
                  </td>
                  <td className="previous-cell">
                    {s.previous ? `${s.previous.weightKg}kg x ${s.previous.reps}` : '—'}
                  </td>
                  <td>
                    <input
                      type="number"
                      step="0.5"
                      value={s.weightKg}
                      onChange={(e) => updateSet(exIdx, setIdx, 'weightKg', e.target.value)}
                      className={s.previous && !s.completed ? 'prefilled' : ''}
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      value={s.reps}
                      onChange={(e) => updateSet(exIdx, setIdx, 'reps', e.target.value)}
                      className={s.previous && !s.completed ? 'prefilled' : ''}
                    />
                  </td>
                  <td>
                    <button
                      type="button"
                      className={s.completed ? 'check-btn checked' : 'check-btn'}
                      onClick={() => toggleComplete(exIdx, setIdx)}
                      aria-label={s.completed ? 'Mark set incomplete' : 'Mark set complete'}
                    >
                      ✓
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="add-set-btn" onClick={() => addSet(exIdx)}>+ Add set</button>
        </div>
      ))}
    </main>
  )
}
