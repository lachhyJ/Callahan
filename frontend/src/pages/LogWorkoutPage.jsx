import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createWorkoutSession, getExercises } from '../api/client'

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

export default function LogWorkoutPage() {
  const [exercises, setExercises] = useState([])
  const [date, setDate] = useState(todayIso())
  const [notes, setNotes] = useState('')
  const [sets, setSets] = useState([])
  const [exerciseId, setExerciseId] = useState('')
  const [reps, setReps] = useState('')
  const [weightKg, setWeightKg] = useState('')
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    getExercises()
      .then((list) => {
        setExercises(list)
        if (list.length > 0) setExerciseId(String(list[0].id))
      })
      .catch((err) => setError(err.message))
  }, [])

  function addSet() {
    if (!exerciseId || !reps || !weightKg) return
    const exercise = exercises.find((e) => e.id === Number(exerciseId))
    setSets([
      ...sets,
      {
        exerciseId: Number(exerciseId),
        exerciseName: exercise?.name ?? 'Unknown',
        reps: Number(reps),
        weightKg: Number(weightKg),
        setOrder: sets.length + 1,
      },
    ])
    setReps('')
    setWeightKg('')
  }

  function removeSet(index) {
    const set = sets[index]
    if (!window.confirm(`Remove set ${set.setOrder} of ${set.exerciseName}?`)) return
    setSets(sets.filter((_, i) => i !== index).map((s, i) => ({ ...s, setOrder: i + 1 })))
  }

  async function handleSave() {
    setError(null)
    setSaving(true)
    try {
      await createWorkoutSession({
        date,
        notes: notes || null,
        sets: sets.map(({ exerciseId, reps, weightKg, setOrder }) => ({ exerciseId, reps, weightKg, setOrder })),
      })
      navigate('/history', { state: { savedMessage: 'Workout saved' } })
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="page">
      <h1>Log workout</h1>
      {error && <p className="error">{error}</p>}

      <label>
        Date
        <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
      </label>

      <div className="set-builder">
        <select value={exerciseId} onChange={(e) => setExerciseId(e.target.value)}>
          {exercises.map((ex) => (
            <option key={ex.id} value={ex.id}>
              {ex.name} ({ex.category})
            </option>
          ))}
        </select>
        <input type="number" placeholder="Reps" aria-label="Reps" value={reps} onChange={(e) => setReps(e.target.value)} />
        <input type="number" placeholder="Weight (kg)" aria-label="Weight in kilograms" step="0.5" value={weightKg} onChange={(e) => setWeightKg(e.target.value)} />
        <button type="button" onClick={addSet}>Add set</button>
      </div>

      {sets.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead>
              <tr><th>#</th><th>Exercise</th><th>Reps</th><th>Weight (kg)</th><th /></tr>
            </thead>
            <tbody>
              {sets.map((s, i) => (
                <tr key={i}>
                  <td>{s.setOrder}</td>
                  <td>{s.exerciseName}</td>
                  <td>{s.reps}</td>
                  <td>{s.weightKg}</td>
                  <td><button type="button" onClick={() => removeSet(i)}>Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <label>
        Notes
        <textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
      </label>

      <button type="button" onClick={handleSave} disabled={saving || sets.length === 0}>
        {saving ? 'Saving…' : 'Save session'}
      </button>
    </main>
  )
}
