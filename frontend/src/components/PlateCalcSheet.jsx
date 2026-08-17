import { useEffect, useRef, useState } from 'react'
import {
  BAR_PRESETS,
  PLATE_SETS,
  calculatePlates,
  clearCustomEquipment,
  clearVisibilityOverride,
  getAvailablePlates,
  getCustomEquipment,
  getVisibilityOverride,
  looksPlateLoaded,
  setAvailablePlates,
  setCustomEquipment,
  setVisibilityOverride,
} from '../plateCalc'

const SAVED_BAR = 'saved'
const CUSTOM_BAR = 'custom'
const DEFAULT_BAR_KG = BAR_PRESETS.kg[0].value

// Bar sleeve + a stack of plate blocks, one side only (the other side is a
// mirror image of loading, not a different number) — same visual idea as
// Hevy's plate calculator, height-scaled per plate so the bigger ones read
// as bigger at a glance.
function BarDiagram({ breakdown, maxPlate }) {
  const plates = breakdown.flatMap(({ plate, count }) => Array(count).fill(plate))
  const plateWidth = 26
  const gap = 5
  const sleeveWidth = 80
  const width = sleeveWidth + plates.length * (plateWidth + gap)
  const height = 130
  const midY = height / 2

  return (
    <svg viewBox={`0 0 ${Math.max(width, sleeveWidth + 45)} ${height}`} width="100%" height="115" preserveAspectRatio="xMinYMid meet">
      <rect x="0" y={midY - 9} width={sleeveWidth} height="18" rx="4" fill="var(--border)" />
      {plates.map((plate, i) => {
        const plateHeight = 46 + (plate / maxPlate) * 58
        const x = sleeveWidth + i * (plateWidth + gap)
        return (
          <g key={i}>
            <rect x={x} y={midY - plateHeight / 2} width={plateWidth} height={plateHeight} rx="4" fill="var(--accent)" />
            <text
              x={x + plateWidth / 2}
              y={midY}
              textAnchor="middle"
              dominantBaseline="central"
              fontSize="11"
              fontWeight="700"
              fill="white"
              transform={`rotate(-90 ${x + plateWidth / 2} ${midY})`}
            >
              {plate}
            </text>
          </g>
        )
      })}
    </svg>
  )
}

export default function PlateCalcSheet({ exerciseId, exerciseName, targetWeightKg, onClose }) {
  const open = exerciseId !== null && exerciseId !== undefined
  const [savedEquipment, setSavedEquipment] = useState(null)
  const [selection, setSelection] = useState(String(DEFAULT_BAR_KG))
  const [barWeightKg, setBarWeightKg] = useState(DEFAULT_BAR_KG)
  const [customName, setCustomName] = useState('')
  const [customWeight, setCustomWeight] = useState('')
  const [visible, setVisible] = useState(true)
  const [hasOverride, setHasOverride] = useState(false)
  const [availablePlates, setAvailablePlatesState] = useState(() => getAvailablePlates('kg'))
  const sheetRef = useRef(null)

  // Re-sync to this exercise's saved bar whenever the sheet is opened for a
  // (possibly different) exercise, rather than carrying over whatever was
  // selected for the previous one. Available plates are device-wide, not
  // per-exercise, so they only need reloading in case another tab/session
  // changed them — cheap enough to just always refresh on open.
  useEffect(() => {
    if (!open) return
    const saved = getCustomEquipment(exerciseId)
    setSavedEquipment(saved)
    setSelection(saved ? SAVED_BAR : String(DEFAULT_BAR_KG))
    setBarWeightKg(saved ? saved.kg : DEFAULT_BAR_KG)
    setCustomName('')
    setCustomWeight('')
    const override = getVisibilityOverride(exerciseId)
    setHasOverride(override !== null)
    setVisible(override ? override === 'shown' : looksPlateLoaded(exerciseName))
    setAvailablePlatesState(getAvailablePlates('kg'))
  }, [open, exerciseId, exerciseName])

  useEffect(() => {
    if (!open) return
    function handleKeyDown(e) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onClose])

  function selectSaved() {
    if (!savedEquipment) return
    setSelection(SAVED_BAR)
    setBarWeightKg(savedEquipment.kg)
  }

  function selectPreset(kg) {
    setSelection(String(kg))
    setBarWeightKg(kg)
  }

  function openCustomForm() {
    setSelection(CUSTOM_BAR)
    setCustomName(savedEquipment?.name ?? '')
    setCustomWeight(savedEquipment ? String(savedEquipment.kg) : '')
  }

  function handleCustomWeightChange(e) {
    setCustomWeight(e.target.value)
    const kg = Number(e.target.value)
    if (!Number.isNaN(kg) && kg > 0) setBarWeightKg(kg)
  }

  function handleSaveCustom() {
    const kg = Number(customWeight)
    if (Number.isNaN(kg) || kg <= 0) return
    const entry = { name: customName.trim(), kg }
    setCustomEquipment(exerciseId, entry)
    setSavedEquipment(entry)
    setSelection(SAVED_BAR)
    setBarWeightKg(kg)
  }

  function handleRemoveSaved() {
    clearCustomEquipment(exerciseId)
    setSavedEquipment(null)
    setSelection(String(DEFAULT_BAR_KG))
    setBarWeightKg(DEFAULT_BAR_KG)
    setCustomName('')
    setCustomWeight('')
  }

  function toggleVisible() {
    const next = !visible
    setVisibilityOverride(exerciseId, next)
    setVisible(next)
    setHasOverride(true)
  }

  function resetToAutomatic() {
    clearVisibilityOverride(exerciseId)
    setVisible(looksPlateLoaded(exerciseName))
    setHasOverride(false)
  }

  function togglePlateAvailable(plate) {
    const next = availablePlates.includes(plate)
      ? availablePlates.filter((p) => p !== plate)
      : PLATE_SETS.kg.filter((p) => availablePlates.includes(p) || p === plate)
    setAvailablePlatesState(next)
    setAvailablePlates('kg', next)
  }

  const target = Number(targetWeightKg)
  const hasTarget = targetWeightKg !== '' && targetWeightKg !== undefined && !Number.isNaN(target)
  const belowBar = hasTarget && target < barWeightKg
  const perSide = hasTarget && !belowBar ? (target - barWeightKg) / 2 : 0
  const result = hasTarget && !belowBar ? calculatePlates(perSide, availablePlates) : null
  const maxPlate = Math.max(...(availablePlates.length > 0 ? availablePlates : PLATE_SETS.kg))

  return (
    <>
      <div className={open ? 'sheet-backdrop visible' : 'sheet-backdrop'} onClick={onClose} />
      <div ref={sheetRef} className={open ? 'day-detail-sheet plate-calc-sheet open' : 'day-detail-sheet plate-calc-sheet'} role="dialog" aria-modal="true" aria-label="Plate calculator">
        {open && (
          <>
            <div className="day-detail-sheet-header">
              <div>
                <strong>Plate calculator</strong>
                {exerciseName && <p className="plate-calc-sheet-subtitle">{exerciseName}</p>}
              </div>
              <button type="button" className="sheet-close-btn" onClick={onClose} aria-label="Close">×</button>
            </div>

            <p className="plate-calc-sheet-target">
              Target weight: {hasTarget ? `${targetWeightKg}kg` : '—'}
            </p>

            {belowBar && <p className="error">Below the bar's own weight.</p>}
            {!hasTarget && <p className="plate-calc-popover-hint">Enter a weight on the set to see plates.</p>}

            {result && (
              <>
                <BarDiagram breakdown={result.breakdown} maxPlate={maxPlate} />
                <p className="plate-calc-sheet-perside">{perSide}kg per side</p>
                {result.breakdown.length === 0 && <p className="plate-calc-popover-hint">Just the bar — no plates needed.</p>}
                {result.remainder > 0 && (
                  <p className="error">Can't hit that exactly with your available plates — {result.remainder}kg short per side.</p>
                )}
              </>
            )}

            <div className="plate-calc-sheet-bars">
              <span className="plate-calc-sheet-label">Bar / sled</span>
              <div className="plate-calc-chip-row">
                {savedEquipment && (
                  <button
                    type="button"
                    className={selection === SAVED_BAR ? 'plate-calc-chip active' : 'plate-calc-chip'}
                    onClick={selectSaved}
                  >
                    {savedEquipment.name || `This bar (${savedEquipment.kg}kg)`}
                  </button>
                )}
                {BAR_PRESETS.kg.map((preset) => (
                  <button
                    key={preset.value}
                    type="button"
                    className={selection === String(preset.value) ? 'plate-calc-chip active' : 'plate-calc-chip'}
                    onClick={() => selectPreset(preset.value)}
                  >
                    {preset.label}
                  </button>
                ))}
                <button
                  type="button"
                  className={selection === CUSTOM_BAR ? 'plate-calc-chip active' : 'plate-calc-chip'}
                  onClick={openCustomForm}
                >
                  {savedEquipment ? 'Edit' : 'Custom'}
                </button>
              </div>
              {selection === CUSTOM_BAR && (
                <div className="plate-calc-custom-form">
                  <input
                    type="text"
                    placeholder={`Name (optional), e.g. ${exerciseName ? `${exerciseName} bar` : 'Trap bar'}`}
                    value={customName}
                    onChange={(e) => setCustomName(e.target.value)}
                  />
                  <input
                    type="text"
                    inputMode="decimal"
                    placeholder="Weight (kg)"
                    value={customWeight}
                    onChange={handleCustomWeightChange}
                    autoFocus
                  />
                  <div className="plate-calc-custom-actions">
                    <button type="button" className="secondary-btn" onClick={handleSaveCustom}>
                      Save for {exerciseName || 'this exercise'}
                    </button>
                    {savedEquipment && (
                      <button type="button" className="plate-calc-custom-remove" onClick={handleRemoveSaved}>
                        Remove
                      </button>
                    )}
                  </div>
                </div>
              )}
            </div>

            <div className="plate-calc-sheet-bars">
              <span className="plate-calc-sheet-label">Plates you have (kg)</span>
              <div className="plate-calc-chip-row">
                {PLATE_SETS.kg.map((plate) => (
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

            <div className="plate-calc-visibility-row">
              <button type="button" className="plate-calc-hide-toggle" onClick={toggleVisible}>
                {visible
                  ? `Hide the calculator button for ${exerciseName || 'this exercise'}`
                  : `Show the calculator button for ${exerciseName || 'this exercise'}`}
              </button>
              {hasOverride && (
                <button type="button" className="plate-calc-hide-toggle" onClick={resetToAutomatic}>
                  Reset to automatic
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </>
  )
}
