import { useEffect, useState } from 'react'
import { getTrainingMode, setTrainingMode } from '../api/client'
import { clearCacheKey } from '../swrCache'

const MODES = [
  { value: 'InSeason', label: 'In-season', hint: 'Gym A / B' },
  { value: 'NoClub', label: 'Off-season', hint: 'Gym 1 / 2 / 3' },
]

// Which block of the program you're in. "Off-season" is the label for the doc's no-club
// block (NoClub internally): Christmas break, off-season, any week without club. Flipped by
// hand, since block dates are approximate and shift when the club confirms. Decides which
// gym templates "Start a workout" offers; rendered on the Dashboard and on the picker
// itself, where a wrong list is noticed.
export default function TrainingModeSetting({ onChange }) {
  const [mode, setMode] = useState(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  useEffect(() => {
    getTrainingMode().then((m) => setMode(m.mode)).catch(() => {})
  }, [])

  async function choose(next) {
    if (next === mode || saving) return
    setError(null)
    setSaving(true)
    try {
      const res = await setTrainingMode(next)
      setMode(res.mode)
      clearCacheKey('workouttemplates')
      onChange?.(res.mode)
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  if (!mode) return null

  return (
    <div className="mode-setting section-gap">
      <span className="mode-setting-label" id="training-mode-label">Training block</span>
      <div className="mode-toggle" role="group" aria-labelledby="training-mode-label">
        {MODES.map((m) => (
          <button
            key={m.value}
            type="button"
            className={`mode-toggle-btn${m.value === mode ? ' mode-toggle-btn-on' : ''}`}
            aria-pressed={m.value === mode}
            disabled={saving}
            onClick={() => choose(m.value)}
          >
            {m.label}
            <small>{m.hint}</small>
          </button>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
