import { useEffect, useState } from 'react'
import { BAR_PRESETS, PLATE_SETS, calculatePlates, getAvailablePlates, setAvailablePlates } from '../plateCalc'

const CUSTOM_BAR = 'custom'

export default function PlateCalculatorPage() {
  const [unit, setUnit] = useState('kg')
  const [targetWeight, setTargetWeight] = useState('')
  const [barPreset, setBarPreset] = useState(String(BAR_PRESETS.kg[0].value))
  const [customBarWeight, setCustomBarWeight] = useState('')
  const [availablePlates, setAvailablePlatesState] = useState(() => getAvailablePlates('kg'))

  useEffect(() => {
    setAvailablePlatesState(getAvailablePlates(unit))
  }, [unit])

  function changeUnit(nextUnit) {
    if (nextUnit === unit) return
    setUnit(nextUnit)
    setBarPreset(String(BAR_PRESETS[nextUnit][0].value))
    setCustomBarWeight('')
  }

  function togglePlateAvailable(plate) {
    const next = availablePlates.includes(plate)
      ? availablePlates.filter((p) => p !== plate)
      : PLATE_SETS[unit].filter((p) => availablePlates.includes(p) || p === plate)
    setAvailablePlatesState(next)
    setAvailablePlates(unit, next)
  }

  const barWeight = barPreset === CUSTOM_BAR ? customBarWeight : barPreset

  const target = Number(targetWeight)
  const bar = Number(barWeight)
  const hasValidInputs = targetWeight !== '' && barWeight !== '' && !Number.isNaN(target) && !Number.isNaN(bar) && bar > 0
  const perSide = hasValidInputs ? (target - bar) / 2 : 0
  const belowBar = hasValidInputs && target < bar
  const result = hasValidInputs && !belowBar ? calculatePlates(perSide, availablePlates) : null

  return (
    <main className="page page-narrow">
      <h1>Plate calculator</h1>

      <div className="unit-toggle-row">
        <button type="button" className={unit === 'kg' ? 'secondary-btn active' : 'secondary-btn'} onClick={() => changeUnit('kg')}>kg</button>
        <button type="button" className={unit === 'lb' ? 'secondary-btn active' : 'secondary-btn'} onClick={() => changeUnit('lb')}>lb</button>
      </div>

      <label>
        Target weight ({unit})
        <input
          type="text"
          inputMode="decimal"
          placeholder={unit === 'kg' ? 'e.g. 100' : 'e.g. 225'}
          value={targetWeight}
          onChange={(e) => setTargetWeight(e.target.value)}
        />
      </label>

      <label>
        Bar weight ({unit})
        <select value={barPreset} onChange={(e) => setBarPreset(e.target.value)}>
          {BAR_PRESETS[unit].map((preset) => (
            <option key={preset.value} value={preset.value}>{preset.label}</option>
          ))}
          <option value={CUSTOM_BAR}>Custom</option>
        </select>
      </label>

      {barPreset === CUSTOM_BAR && (
        <label>
          Custom bar weight ({unit})
          <input
            type="text"
            inputMode="decimal"
            value={customBarWeight}
            onChange={(e) => setCustomBarWeight(e.target.value)}
          />
        </label>
      )}

      <div className="plate-calc-sheet-bars">
        <span className="plate-calc-sheet-label">Plates you have ({unit})</span>
        <div className="plate-calc-chip-row">
          {PLATE_SETS[unit].map((plate) => (
            <button
              key={plate}
              type="button"
              className={availablePlates.includes(plate) ? 'plate-calc-chip active' : 'plate-calc-chip'}
              onClick={() => togglePlateAvailable(plate)}
            >
              {plate}
            </button>
          ))}
        </div>
      </div>

      {belowBar && (
        <p className="error">Target weight is less than the bar itself — nothing to load.</p>
      )}

      {result && (
        <div className="plate-calc-result section-gap">
          <p className="page-subtitle">{perSide}{unit} per side</p>
          {result.breakdown.length === 0 && (
            <p>No plates needed — just the bar.</p>
          )}
          {result.breakdown.length > 0 && (
            <ul className="plate-calc-breakdown">
              {result.breakdown.map(({ plate, count }) => (
                <li key={plate}>
                  <span className="plate-calc-plate">{plate}{unit}</span>
                  <span>× {count} per side</span>
                </li>
              ))}
            </ul>
          )}
          {result.remainder > 0 && (
            <p className="error">
              Can't hit that exactly with your available plates — {result.remainder}{unit} short per side.
            </p>
          )}
        </div>
      )}
    </main>
  )
}
