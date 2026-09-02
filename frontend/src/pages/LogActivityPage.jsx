import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createActivity } from '../api/client'
import { trainingDayIso } from '../dateUtils'

export default function LogActivityPage() {
  const [type, setType] = useState('Running')
  // Defaults to the training day (see trainingDayIso) — a run started just
  // after midnight belongs to the day before. The field stays visible and
  // editable, so the rare post-midnight Ultimate entry is a one-tap change.
  const [date, setDate] = useState(() => trainingDayIso())
  const [distanceKm, setDistanceKm] = useState('')
  const [minutes, setMinutes] = useState('')
  const [seconds, setSeconds] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState(null)
  const [saving, setSaving] = useState(false)
  const navigate = useNavigate()

  async function handleSave(e) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    try {
      const durationSeconds = (Number(minutes) || 0) * 60 + (Number(seconds) || 0)
      await createActivity({
        date,
        type,
        distanceKm: type === 'Running' ? Number(distanceKm) : null,
        durationSeconds,
        notes: notes || null,
      })
      navigate('/dashboard', { state: { savedMessage: 'Activity saved' } })
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="page page-narrow">
      <h1>Log activity</h1>
      {error && <p className="error">{error}</p>}
      <form onSubmit={handleSave}>
        <label>
          Type
          <select value={type} onChange={(e) => setType(e.target.value)}>
            <option value="Running">Running</option>
            <option value="Ultimate">Ultimate</option>
          </select>
        </label>
        <label>
          Date
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </label>
        {type === 'Running' && (
          <label>
            Distance (km)
            <input type="number" step="0.01" value={distanceKm} onChange={(e) => setDistanceKm(e.target.value)} required />
          </label>
        )}
        <label>
          Duration
          <div className="duration-inputs">
            <input type="number" placeholder="min" aria-label="Minutes" value={minutes} onChange={(e) => setMinutes(e.target.value)} />
            <input type="number" placeholder="sec" aria-label="Seconds" value={seconds} onChange={(e) => setSeconds(e.target.value)} />
          </div>
        </label>
        <label>
          Notes
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
        <button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save activity'}</button>
      </form>
    </main>
  )
}
