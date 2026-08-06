import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createRunningSession } from '../api/client'

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

export default function LogRunPage() {
  const [date, setDate] = useState(todayIso())
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
      await createRunningSession({
        date,
        distanceKm: Number(distanceKm),
        durationSeconds,
        notes: notes || null,
      })
      navigate('/history', { state: { savedMessage: 'Run saved' } })
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="page page-narrow">
      <h1>Log run</h1>
      {error && <p className="error">{error}</p>}
      <form onSubmit={handleSave}>
        <label>
          Date
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </label>
        <label>
          Distance (km)
          <input type="number" step="0.01" value={distanceKm} onChange={(e) => setDistanceKm(e.target.value)} required />
        </label>
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
        <button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save run'}</button>
      </form>
    </main>
  )
}
