import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { createWorkoutSession, getFinishers, startWorkoutTemplate } from '../api/client'
import { clearActiveWorkout, loadActiveWorkout, saveActiveWorkout } from '../activeWorkout'

const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }
const SET_TYPE_OPTIONS = ['Warmup', 'Normal', 'Failure', 'Drop']

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function formatDuration(ms) {
  const totalSeconds = Math.floor(ms / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
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

function exerciseFromStart(ex) {
  return {
    exerciseId: ex.exerciseId,
    exerciseName: ex.exerciseName,
    targetReps: ex.targetReps,
    notes: '',
    sets: buildInitialSets(ex.targetSets, ex.previousSets),
  }
}

function completedSetsFor(ex) {
  return ex.sets.filter((s) => s.completed && s.reps !== '')
}

export default function ActiveWorkoutPage() {
  const { templateId } = useParams()
  const [templateName, setTemplateName] = useState('')
  const [exercises, setExercises] = useState(null)
  const [finishers, setFinishers] = useState([])
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const [openTypeMenu, setOpenTypeMenu] = useState(null)
  const [startedAt, setStartedAt] = useState(() => new Date())
  const [now, setNow] = useState(() => new Date())
  const [showSummary, setShowSummary] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    const saved = loadActiveWorkout()
    if (saved && saved.templateId === Number(templateId)) {
      setTemplateName(saved.templateName)
      setExercises(saved.exercises)
      setStartedAt(new Date(saved.startedAt))
    } else {
      startWorkoutTemplate(templateId)
        .then((data) => {
          setTemplateName(data.templateName)
          setExercises(data.exercises.map(exerciseFromStart))
        })
        .catch((err) => setError(err.message))
    }

    getFinishers().then(setFinishers).catch(() => {})
  }, [templateId])

  useEffect(() => {
    if (!exercises) return
    saveActiveWorkout({ templateId: Number(templateId), templateName, exercises, startedAt: startedAt.toISOString() })
  }, [exercises, templateName, templateId, startedAt])

  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(interval)
  }, [])

  const stats = useMemo(() => {
    if (!exercises) return { volume: 0, setCount: 0 }
    let volume = 0
    let setCount = 0
    for (const ex of exercises) {
      for (const s of ex.sets) {
        if (!s.completed) continue
        setCount += 1
        if (s.type !== 'Warmup') {
          volume += (Number(s.weightKg) || 0) * (Number(s.reps) || 0)
        }
      }
    }
    return { volume, setCount }
  }, [exercises])

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
    const set = exercises[exIdx].sets[setIdx]
    if (!set.completed && set.reps === '') {
      setError('Enter reps before marking a set complete.')
      return
    }
    setError(null)
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
    setExercises((prev) => {
      const ex = prev[exIdx]
      const remainingSets = ex.sets.filter((_, j) => j !== setIdx).map((s, j) => ({ ...s, setOrder: j + 1 }))
      if (remainingSets.length === 0) {
        return prev.filter((_, i) => i !== exIdx)
      }
      return prev.map((e, i) => (i !== exIdx ? e : { ...e, sets: remainingSets }))
    })
    setOpenTypeMenu(null)
  }

  function removeExercise(exIdx) {
    setExercises((prev) => prev.filter((_, i) => i !== exIdx))
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

  function addFinisher(finisher) {
    setExercises((prev) => [...prev, exerciseFromStart(finisher)])
  }

  async function handleSave() {
    setError(null)
    setSaving(true)
    try {
      const sets = exercises.flatMap((ex) =>
        completedSetsFor(ex).map((s) => ({
          exerciseId: ex.exerciseId,
          reps: Number(s.reps),
          weightKg: s.weightKg === '' ? 0 : Number(s.weightKg),
          setOrder: s.setOrder,
          setType: s.type,
        }))
      )

      const exerciseNotes = exercises
        .filter((ex) => ex.notes.trim() !== '')
        .map((ex) => ({ exerciseId: ex.exerciseId, notes: ex.notes.trim() }))

      await createWorkoutSession({
        date: todayIso(),
        notes: null,
        workoutTemplateId: Number(templateId),
        startedAt: startedAt.toISOString(),
        finishedAt: new Date().toISOString(),
        sets,
        exerciseNotes,
      })
      clearActiveWorkout()
      navigate('/history')
    } catch (err) {
      setError(err.message)
      setSaving(false)
    }
  }

  function handleDiscard() {
    clearActiveWorkout()
    navigate('/')
  }

  if (error && !exercises) return <main className="page"><p className="error">{error}</p></main>
  if (!exercises) return <main className="page"><p>Loading…</p></main>

  if (showSummary) {
    const exercisesWithCompletedSets = exercises
      .map((ex) => ({ ex, sets: completedSetsFor(ex) }))
      .filter(({ sets }) => sets.length > 0)

    return (
      <main className="page">
        <h1>Finish {templateName}?</h1>
        <div className="live-stats">
          <span>{formatDuration(now - startedAt)}</span>
          <span>{stats.volume.toLocaleString()} kg</span>
          <span>{stats.setCount} set{stats.setCount === 1 ? '' : 's'}</span>
        </div>
        {error && <p className="error">{error}</p>}
        {exercisesWithCompletedSets.length === 0 && <p>No completed sets — nothing to save.</p>}
        {exercisesWithCompletedSets.map(({ ex, sets }) => (
          <div key={ex.exerciseId} className="summary-exercise">
            <h2>{ex.exerciseName}</h2>
            <ul className="summary-set-list">
              {sets.map((s, i) => (
                <li key={i}>
                  {s.weightKg === '' ? 0 : s.weightKg}kg × {s.reps}{s.type !== 'Normal' ? ` (${s.type})` : ''}
                </li>
              ))}
            </ul>
            {ex.notes && <p className="notes">{ex.notes}</p>}
          </div>
        ))}
        <div className="summary-actions">
          <button type="button" className="back-btn" onClick={() => setShowSummary(false)}>← Back to workout</button>
          <button type="button" onClick={handleSave} disabled={saving || stats.setCount === 0}>
            {saving ? 'Saving…' : 'Save workout'}
          </button>
          <button type="button" className="discard-btn" onClick={handleDiscard}>Discard workout</button>
        </div>
      </main>
    )
  }

  const addedExerciseIds = new Set(exercises.map((ex) => ex.exerciseId))
  const availableFinishers = finishers.filter((f) => !addedExerciseIds.has(f.exerciseId))

  return (
    <main className="page">
      <div className="active-workout-header">
        <h1>{templateName}</h1>
        <button type="button" onClick={() => setShowSummary(true)}>Finish</button>
      </div>
      <div className="live-stats">
        <span>{formatDuration(now - startedAt)}</span>
        <span>{stats.volume.toLocaleString()} kg</span>
        <span>{stats.setCount} set{stats.setCount === 1 ? '' : 's'}</span>
      </div>
      {error && <p className="error">{error}</p>}

      {exercises.map((ex, exIdx) => (
        <div key={`${ex.exerciseId}-${exIdx}`} className="exercise-card">
          <div className="exercise-card-header">
            <h2>{ex.exerciseName}</h2>
            <button type="button" className="remove-exercise-btn" onClick={() => removeExercise(exIdx)} aria-label={`Remove ${ex.exerciseName}`}>
              Remove
            </button>
          </div>
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
                      placeholder="0"
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

      {availableFinishers.length > 0 && (
        <div className="finisher-list">
          <h2 className="finisher-heading">Finishers — pick 1–2</h2>
          {availableFinishers.map((f) => (
            <button key={f.exerciseId} type="button" className="finisher-item" onClick={() => addFinisher(f)}>
              <span>{f.exerciseName}</span>
              <span className="finisher-meta">{f.targetSets} × {f.targetReps}</span>
            </button>
          ))}
        </div>
      )}
    </main>
  )
}
