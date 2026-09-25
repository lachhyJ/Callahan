import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import {
  BAR_PRESETS,
  DUMBBELL_STEPS_KG,
  PLATE_SETS,
  calculatePlateDelta,
  calculatePlates,
  clearCustomEquipment,
  roundDisplay,
  roundToStep,
  clearEquipmentTypeOverride,
  getAvailableDumbbells,
  getAvailablePlates,
  getCustomEquipment,
  getEquipmentTypeOverride,
  guessEquipmentType,
  nearestDumbbells,
  setAvailableDumbbells,
  setAvailablePlates,
  setCustomEquipment,
  setEquipmentTypeOverride,
} from '../plateCalc'
import { KEYBOARD_ACCESSORY_HEIGHT, useKeyboardInset } from '../useKeyboardInset'

const SAVED_BAR = 'saved'
const CUSTOM_BAR = 'custom'
const DEFAULT_BAR_KG = BAR_PRESETS.kg[0].value

// Bar sleeve + a stack of plate blocks, one side only (the other side is a
// mirror image of loading, not a different number) — same visual idea as
// Hevy's plate calculator, height-scaled per plate so the bigger ones read
// as bigger at a glance. Numbers sit upright rather than rotated, so plates
// are wide enough to fit them without shrinking the text down to nothing.
// Colors are picked for this app's dark theme specifically (the only theme
// this view is used in) rather than pulled from the general button palette,
// which reads as too low-contrast against a near-black sheet here.
function BarDiagram({ breakdown, maxPlate, barWeightKg }) {
  const plates = breakdown.flatMap(({ plate, count }) => Array(count).fill(plate))
  const plateWidth = 38
  const gap = 7
  const sleeveWidth = 118
  const width = sleeveWidth + plates.length * (plateWidth + gap)
  const height = 150
  const midY = height / 2

  return (
    <svg viewBox={`0 0 ${Math.max(width, sleeveWidth + 55)} ${height}`} width="100%" height="130" preserveAspectRatio="xMidYMid meet">
      <rect x="0" y={midY - 14} width={sleeveWidth} height="28" rx="5" fill="#9497a6" />
      <text
        x={sleeveWidth / 2}
        y={midY}
        textAnchor="middle"
        dominantBaseline="central"
        fontSize="15"
        fontWeight="800"
        fill="#16171d"
      >
        {barWeightKg}
      </text>
      {plates.map((plate, i) => {
        const plateHeight = 58 + (plate / maxPlate) * 76
        const x = sleeveWidth + i * (plateWidth + gap)
        return (
          <g key={i}>
            <rect x={x} y={midY - plateHeight / 2} width={plateWidth} height={plateHeight} rx="6" fill="#b453ff" />
            <text
              x={x + plateWidth / 2}
              y={midY}
              textAnchor="middle"
              dominantBaseline="central"
              fontSize="15"
              fontWeight="800"
              fill="#fbfaff"
            >
              {plate}
            </text>
          </g>
        )
      })}
    </svg>
  )
}

// Shared by barbell and added-weight modes — both load from the same gym
// plate set, so the picker is one component rather than two copies.
function PlatesYouHave({ available, onToggle }) {
  return (
    <div className="plate-calc-sheet-bars">
      <span className="plate-calc-sheet-label">Plates you have (kg)</span>
      <div className="plate-calc-chip-row">
        {PLATE_SETS.kg.map((plate) => (
          <button
            key={plate}
            type="button"
            className={available.includes(plate) ? 'plate-calc-chip active' : 'plate-calc-chip'}
            onClick={() => onToggle(plate)}
          >
            {plate}
          </button>
        ))}
      </div>
    </div>
  )
}

// "From here" plate-swap note against the last completed set on this
// exercise — add/remove per side (barbell) or on the stack (added), rather
// than making the athlete re-derive the change from two full breakdowns.
function PlateDeltaNote({ delta, perSide }) {
  if (!delta) return null
  const { toAdd, toRemove } = delta
  if (toAdd.length === 0 && toRemove.length === 0) {
    return <p className="plate-calc-delta plate-calc-popover-hint">Same as last set — no change.</p>
  }
  const suffix = perSide ? ' per side' : ''
  // Remove listed before add — you strip unwanted plates off before loading
  // new ones, so the note reads in the order you'd actually do it.
  return (
    <p className="plate-calc-delta">
      {toRemove.length > 0 && (
        <>Remove {toRemove.map(({ plate, count }) => `${count}×${plate}kg`).join(', ')}{suffix}</>
      )}
      {toAdd.length > 0 && toRemove.length > 0 && ' · '}
      {toAdd.length > 0 && (
        <>Add {toAdd.map(({ plate, count }) => `${count}×${plate}kg`).join(', ')}{suffix}</>
      )}
    </p>
  )
}

// Rest of the exercise's plate plan, chained off already-known set weights —
// see exercisePlan in the main component for what "chained" means here.
function ExercisePlanNote({ plan, perSide }) {
  if (!plan || plan.upcomingSteps.length === 0) return null
  const suffix = perSide ? ' per side' : ''
  return (
    <div className="plate-calc-exercise-plan">
      <span className="plate-calc-sheet-label">Rest of this exercise</span>
      <ol>
        {plan.upcomingSteps.map(({ toAdd, toRemove }, i) => (
          <li key={i}>
            {toRemove.length === 0 && toAdd.length === 0 && 'No change'}
            {toRemove.length > 0 && <>Remove {toRemove.map(({ plate, count }) => `${count}×${plate}kg`).join(', ')}{suffix}</>}
            {toAdd.length > 0 && toRemove.length > 0 && ' · '}
            {toAdd.length > 0 && <>Add {toAdd.map(({ plate, count }) => `${count}×${plate}kg`).join(', ')}{suffix}</>}
          </li>
        ))}
      </ol>
      <p className="plate-calc-popover-hint">
        {plan.totalMoves} plate {plan.totalMoves === 1 ? 'change' : 'changes'}{suffix} total from here to the end.
      </p>
    </div>
  )
}

const EQUIPMENT_TYPE_LABELS = { barbell: 'Barbell', dumbbell: 'Dumbbell', added: 'Added/Machine', hidden: 'Hide' }

const PROGRESSION_PERCENTAGES = [2.5, 5, 7.5, 10]

export default function PlateCalcSheet({
  exerciseId,
  exerciseName,
  targetWeightKg,
  currentlyLoadedKg,
  upcomingWeightsKg = [],
  previousTargetKg,
  isCurrentSetWarmup = false,
  anyWorkingSetConfirmed = false,
  onApplyWeight,
  onClose,
}) {
  const open = exerciseId !== null && exerciseId !== undefined

  const [equipmentType, setEquipmentTypeState] = useState('barbell')
  const [hasTypeOverride, setHasTypeOverride] = useState(false)
  // Collapsed by default once the exercise already has a confirmed working
  // weight — a percentage jump off "last time" stops being the useful
  // question at that point, but it should stay one click away rather than
  // vanish, in case the wrong jump got applied and needs revisiting.
  const [suggestionsExpanded, setSuggestionsExpanded] = useState(true)

  // Barbell-mode state
  const [savedEquipment, setSavedEquipment] = useState(null)
  const [selection, setSelection] = useState(String(DEFAULT_BAR_KG))
  const [barWeightKg, setBarWeightKg] = useState(DEFAULT_BAR_KG)
  const [customName, setCustomName] = useState('')
  const [customWeight, setCustomWeight] = useState('')
  const [availablePlates, setAvailablePlatesState] = useState(() => getAvailablePlates('kg'))

  // Dumbbell-mode state
  const [availableDumbbells, setAvailableDumbbellsState] = useState(() => getAvailableDumbbells())

  const sheetRef = useRef(null)
  // Pointer-drag state lives in a ref, not React state — the transform is
  // written straight to the DOM on every pointermove so dragging tracks the
  // finger at 60fps without a re-render per frame.
  const dragStateRef = useRef({ dragging: false, startY: 0, currentY: 0 })

  // The sheet is `position: fixed; bottom: 0` against the LAYOUT viewport,
  // which doesn't shrink when the keyboard opens. With no adjustment,
  // focusing a field inside the sheet (custom bar weight, custom bar name)
  // leaves the sheet's lower content sitting behind the keyboard, and even
  // once positioned correctly relative to the *keyboard*, iOS's own native
  // input-accessory bar sits on top of that — see KEYBOARD_ACCESSORY_HEIGHT
  // in useKeyboardInset.js — so lift by both and cap the height so the
  // sheet scrolls internally instead of clipping.
  const rawKeyboardInset = useKeyboardInset()
  const keyboardInset = open ? rawKeyboardInset : 0

  // Re-sync to this exercise's settings whenever the sheet is opened for a
  // (possibly different) exercise, rather than carrying over whatever was
  // selected for the previous one. Device-wide lists (plates/dumbbells) are
  // reloaded too, in case another tab/session changed them — cheap enough
  // to just always refresh on open.
  useEffect(() => {
    if (!open) return
    const typeOverride = getEquipmentTypeOverride(exerciseId)
    setHasTypeOverride(typeOverride !== null)
    setEquipmentTypeState(typeOverride ?? guessEquipmentType(exerciseName))

    const saved = getCustomEquipment(exerciseId)
    setSavedEquipment(saved)
    setSelection(saved ? SAVED_BAR : String(DEFAULT_BAR_KG))
    setBarWeightKg(saved ? saved.kg : DEFAULT_BAR_KG)
    setCustomName('')
    setCustomWeight('')
    setAvailablePlatesState(getAvailablePlates('kg'))
    setAvailableDumbbellsState(getAvailableDumbbells())
  }, [open, exerciseId, exerciseName])

  // Separate from the reset above (which shouldn't re-fire just because
  // applying a suggestion flips anyWorkingSetConfirmed mid-session — that
  // would also wipe the custom-bar-weight form state). Only re-derives the
  // collapsed/expanded default when the sheet opens for a given exercise.
  useEffect(() => {
    if (!open) return
    setSuggestionsExpanded(!anyWorkingSetConfirmed)
  }, [open, exerciseId])

  useEffect(() => {
    if (!open) return
    function handleKeyDown(e) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onClose])

  // Swipe-down-to-close, in addition to the × button/backdrop/Escape already
  // wired above. Only drags downward (negative delta is clamped to 0) — this
  // is a close gesture, not a way to overshoot the sheet upward.
  const DRAG_CLOSE_THRESHOLD_PX = 100

  function handleDragStart(e) {
    if (e.pointerType === 'mouse' && e.button !== 0) return
    // The grab zone now spans the whole non-interactive top of the sheet
    // (handle, title, target weight) — skip starting a drag on the close
    // button itself so its click still fires normally.
    if (e.target.closest('button, a, input')) return
    dragStateRef.current = { dragging: true, startY: e.clientY, currentY: 0 }
    if (sheetRef.current) sheetRef.current.style.transition = 'none'
    e.currentTarget.setPointerCapture(e.pointerId)
  }

  function handleDragMove(e) {
    const state = dragStateRef.current
    if (!state.dragging) return
    const deltaY = Math.max(0, e.clientY - state.startY)
    state.currentY = deltaY
    if (sheetRef.current) sheetRef.current.style.transform = `translateY(${deltaY}px)`
  }

  function handleDragEnd(e) {
    const state = dragStateRef.current
    if (!state.dragging) return
    state.dragging = false
    if (e.currentTarget.hasPointerCapture?.(e.pointerId)) {
      e.currentTarget.releasePointerCapture(e.pointerId)
    }
    if (sheetRef.current) {
      sheetRef.current.style.transition = ''
      sheetRef.current.style.transform = ''
    }
    if (state.currentY > DRAG_CLOSE_THRESHOLD_PX) onClose()
  }

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

  function togglePlateAvailable(plate) {
    const next = availablePlates.includes(plate)
      ? availablePlates.filter((p) => p !== plate)
      : PLATE_SETS.kg.filter((p) => availablePlates.includes(p) || p === plate)
    setAvailablePlatesState(next)
    setAvailablePlates('kg', next)
  }

  function toggleDumbbellAvailable(dumbbell) {
    const next = availableDumbbells.includes(dumbbell)
      ? availableDumbbells.filter((d) => d !== dumbbell)
      : DUMBBELL_STEPS_KG.filter((d) => availableDumbbells.includes(d) || d === dumbbell)
    setAvailableDumbbellsState(next)
    setAvailableDumbbells(next)
  }

  function selectEquipmentType(type) {
    setEquipmentTypeOverride(exerciseId, type)
    setEquipmentTypeState(type)
    setHasTypeOverride(true)
  }

  function resetEquipmentType() {
    clearEquipmentTypeOverride(exerciseId)
    setEquipmentTypeState(guessEquipmentType(exerciseName))
    setHasTypeOverride(false)
  }

  const target = Number(targetWeightKg)
  const hasTarget = targetWeightKg !== '' && targetWeightKg !== undefined && !Number.isNaN(target)

  const belowBar = equipmentType === 'barbell' && hasTarget && target < barWeightKg
  const perSide = equipmentType === 'barbell' && hasTarget && !belowBar ? (target - barWeightKg) / 2 : 0
  const result = equipmentType === 'barbell' && hasTarget && !belowBar ? calculatePlates(perSide, availablePlates) : null
  const maxPlate = Math.max(...(availablePlates.length > 0 ? availablePlates : PLATE_SETS.kg))

  // Added weight hangs off a belt (base weight 0) or loads onto a
  // plate-loaded machine with its own unknown frame weight (e.g. hip
  // thrust) — either way it's a single stack, so no halving, but a
  // machine's frame weight has to come off the target first or the
  // breakdown asks the athlete to load plates for weight the frame
  // already provides. Reuses the same per-exercise custom-equipment
  // storage as barbell mode's bar weight (savedEquipment), since only one
  // equipment type is active for a given exercise at a time.
  const addedBaseKg = equipmentType === 'added' && savedEquipment ? savedEquipment.kg : 0
  const belowBase = equipmentType === 'added' && hasTarget && target < addedBaseKg
  const addedLoad = equipmentType === 'added' && hasTarget && !belowBase ? target - addedBaseKg : 0
  const addedResult =
    equipmentType === 'added' && hasTarget && !belowBase && addedLoad > 0 ? calculatePlates(addedLoad, availablePlates) : null

  // What's still racked from the last completed set on this exercise, so a
  // build-up session (warm-up → working sets, or a straight ramp) shows
  // what to add/remove from there instead of a fresh breakdown from an
  // empty bar every time. Only trusted when it came off the same bar/base
  // weight currently selected — if the athlete just switched bar presets,
  // the "loaded" figure isn't really comparable, so it's dropped and the
  // sheet falls back to the full breakdown.
  const hasLoaded = typeof currentlyLoadedKg === 'number' && !Number.isNaN(currentlyLoadedKg)
  const loadedPerSide =
    equipmentType === 'barbell' && hasLoaded && currentlyLoadedKg >= barWeightKg ? (currentlyLoadedKg - barWeightKg) / 2 : null
  const plateDelta =
    equipmentType === 'barbell' && result && loadedPerSide !== null
      ? calculatePlateDelta(loadedPerSide, perSide, availablePlates)
      : null

  const loadedAddedLoad = equipmentType === 'added' && hasLoaded ? currentlyLoadedKg - addedBaseKg : null
  const addedPlateDelta =
    equipmentType === 'added' && addedResult && loadedAddedLoad !== null && loadedAddedLoad >= 0
      ? calculatePlateDelta(loadedAddedLoad, addedLoad, availablePlates)
      : null

  // Whole-exercise plan: chain this set's target through every later set's
  // already-known weight (whatever's currently in that field, auto-filled
  // or typed), starting from whatever's actually loaded right now if
  // anything is. Each step reuses the same greedy per-weight breakdown as
  // the single-set delta above. Searching alternate ways to build a weight
  // (e.g. keeping a 15 and adding a 10 rather than swapping to 20+5) was
  // measured against real session history and deliberately not built: with
  // the heaviest plate innermost it saves ~3% of plate moves once loading
  // the empty bar and stripping it at the end are counted. Any future
  // version would also have to derive the "currently loaded" baseline from
  // the plan rather than from the last weight's greedy fill.
  const barbellHasBaseline = equipmentType === 'barbell' && loadedPerSide !== null && loadedPerSide !== undefined && loadedPerSide >= 0
  const addedHasBaseline = equipmentType === 'added' && loadedAddedLoad !== null && loadedAddedLoad !== undefined && loadedAddedLoad >= 0
  const barbellChain =
    equipmentType === 'barbell' && result
      ? [barbellHasBaseline ? loadedPerSide : null, perSide, ...upcomingWeightsKg.map((w) => (w - barWeightKg) / 2)].filter(
          (v) => v !== null && v !== undefined && v >= 0,
        )
      : null
  const addedChain =
    equipmentType === 'added' && addedResult
      ? [addedHasBaseline ? loadedAddedLoad : null, addedLoad, ...upcomingWeightsKg.map((w) => w - addedBaseKg)].filter(
          (v) => v !== null && v !== undefined && v >= 0,
        )
      : null
  const exercisePlan = (() => {
    const chain = barbellChain ?? addedChain
    const hasBaseline = barbellHasBaseline || addedHasBaseline
    if (!chain || chain.length < 2) return null
    const steps = []
    for (let i = 1; i < chain.length; i++) {
      steps.push(calculatePlateDelta(chain[i - 1], chain[i], availablePlates))
    }
    // Only drop the first computed transition when chain[0] was actually
    // the "currently loaded" baseline (already shown above as the main
    // delta) — without a baseline, chain[0] *is* the current target, so
    // every computed step here is a genuinely still-upcoming one and none
    // of them were shown elsewhere. Getting this wrong previously dropped a
    // real transition from the visible list while still counting its moves
    // in the total, so the total didn't match what was on screen.
    const upcomingSteps = hasBaseline ? steps.slice(1) : steps
    const totalMoves = upcomingSteps.reduce(
      (sum, { toAdd, toRemove }) =>
        sum + toAdd.reduce((a, { count }) => a + count, 0) + toRemove.reduce((a, { count }) => a + count, 0),
      0,
    )
    return { upcomingSteps, totalMoves }
  })()

  // "What should my next jump be" — percentage suggestions off last
  // session's weight for this exact set (matching how Lachlan already does
  // this by hand: ~1.05x), each rounded to something the current equipment
  // actually lets him load rather than a number that only exists on paper.
  const hasPrevious = typeof previousTargetKg === 'number' && !Number.isNaN(previousTargetKg) && previousTargetKg > 0
  const smallestPlate = availablePlates.length > 0 ? Math.min(...availablePlates) : 1.25
  const progressionSuggestions =
    hasPrevious && equipmentType !== 'hidden'
      ? PROGRESSION_PERCENTAGES.map((pct) => {
          const raw = previousTargetKg * (1 + pct / 100)
          let achievable
          if (equipmentType === 'barbell') {
            achievable = barWeightKg + 2 * roundToStep((raw - barWeightKg) / 2, smallestPlate)
          } else if (equipmentType === 'added') {
            achievable = addedBaseKg + roundToStep(raw - addedBaseKg, smallestPlate)
          } else {
            // dumbbell: snap to the closer of the nearest available step
            // below/above the raw per-dumbbell figure.
            const perDumbbellRaw = raw / 2
            const match = nearestDumbbells(perDumbbellRaw, availableDumbbells)
            const chosen =
              match.exact ??
              (match.below === undefined
                ? match.above
                : match.above === undefined
                ? match.below
                : Math.abs(match.below - perDumbbellRaw) <= Math.abs(match.above - perDumbbellRaw)
                ? match.below
                : match.above)
            achievable = chosen === undefined ? null : chosen * 2
          }
          return { pct, achievable }
        }).filter(({ achievable }) => achievable !== null)
      : []

  const perDumbbell = equipmentType === 'dumbbell' && hasTarget ? target / 2 : null
  const dumbbellMatch = perDumbbell !== null ? nearestDumbbells(perDumbbell, availableDumbbells) : null

  // Portalled to <body>: position:fixed inside .app-content (the scroll
  // container) rides along with a scroll gesture on iOS WKWebView instead
  // of staying pinned to the viewport, only settling into its real spot
  // once scrolling fully stops — same fix ConfirmSheet already needed.
  return createPortal(
    <>
      <div className={open ? 'sheet-backdrop visible' : 'sheet-backdrop'} onClick={onClose} />
      <div
        ref={sheetRef}
        className={open ? 'day-detail-sheet plate-calc-sheet open' : 'day-detail-sheet plate-calc-sheet'}
        style={
          keyboardInset > 0
            ? {
                bottom: keyboardInset + KEYBOARD_ACCESSORY_HEIGHT,
                maxHeight: `calc(100vh - ${keyboardInset + KEYBOARD_ACCESSORY_HEIGHT}px)`,
                overflowY: 'auto',
              }
            : undefined
        }
        role="dialog"
        aria-modal="true"
        aria-label="Plate calculator"
      >
        {open && (
          <>
            <div
              className="plate-calc-sheet-grab-zone"
              onPointerDown={handleDragStart}
              onPointerMove={handleDragMove}
              onPointerUp={handleDragEnd}
              onPointerCancel={handleDragEnd}
            >
              <div className="plate-calc-sheet-handle-row">
                <div className="plate-calc-sheet-handle" />
              </div>
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
            </div>

            {progressionSuggestions.length > 0 && !isCurrentSetWarmup && (
              <div className="plate-calc-sheet-bars">
                <div className="plate-calc-sheet-label-row">
                  <span className="plate-calc-sheet-label">Next jump, off last time ({previousTargetKg}kg)</span>
                  <button
                    type="button"
                    className="plate-calc-suggestions-toggle"
                    onClick={() => setSuggestionsExpanded((v) => !v)}
                  >
                    {suggestionsExpanded ? 'Hide' : 'Show'}
                  </button>
                </div>
                {suggestionsExpanded && (
                <div className="plate-calc-chip-row">
                  {progressionSuggestions.map(({ pct, achievable }) => (
                    <button
                      key={pct}
                      type="button"
                      className="plate-calc-chip"
                      onClick={() => onApplyWeight?.(achievable)}
                    >
                      +{pct}% → {roundDisplay(achievable)}kg
                    </button>
                  ))}
                </div>
                )}
              </div>
            )}

            {equipmentType === 'barbell' && (
              <>
                {belowBar && <p className="error">Below the bar's own weight.</p>}
                {!hasTarget && <p className="plate-calc-popover-hint">Enter a weight on the set to see plates.</p>}

                {result && (
                  <>
                    <BarDiagram breakdown={result.breakdown} maxPlate={maxPlate} barWeightKg={barWeightKg} />
                    <p className="plate-calc-sheet-perside">{roundDisplay(perSide)}kg per side</p>
                    <PlateDeltaNote delta={plateDelta} perSide />
                    {result.breakdown.length === 0 && <p className="plate-calc-popover-hint">Just the bar — no plates needed.</p>}
                    {result.remainder > 0 && (
                      <p className="error">Can't hit that exactly with your available plates — {result.remainder}kg short per side.</p>
                    )}
                    <ExercisePlanNote plan={exercisePlan} perSide />
                  </>
                )}

                <div className="plate-calc-sheet-bars">
                  <span className="plate-calc-sheet-label">Bar / sled</span>
                  <div className="plate-calc-chip-row scroll">
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
                        <button type="button" onClick={handleSaveCustom}>
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

                <PlatesYouHave available={availablePlates} onToggle={togglePlateAvailable} />
              </>
            )}

            {equipmentType === 'dumbbell' && (
              <>
                {!hasTarget && (
                  <p className="plate-calc-popover-hint">Enter the combined weight on the set to see the per-dumbbell split.</p>
                )}
                {perDumbbell !== null && (
                  <>
                    <p className="plate-calc-sheet-perside">{roundDisplay(perDumbbell)}kg per dumbbell</p>
                    {dumbbellMatch.exact !== undefined && (
                      <p className="plate-calc-popover-hint">That's a size you have — grab two.</p>
                    )}
                    {dumbbellMatch.exact === undefined && (dumbbellMatch.below !== undefined || dumbbellMatch.above !== undefined) && (
                      <div className="plate-calc-dumbbell-options">
                        {dumbbellMatch.below !== undefined && (
                          <p>Round down: <strong>{dumbbellMatch.below}kg</strong> each — {roundDisplay(dumbbellMatch.below * 2)}kg combined</p>
                        )}
                        {dumbbellMatch.above !== undefined && (
                          <p>Round up: <strong>{dumbbellMatch.above}kg</strong> each — {roundDisplay(dumbbellMatch.above * 2)}kg combined</p>
                        )}
                      </div>
                    )}
                    {dumbbellMatch.exact === undefined && dumbbellMatch.below === undefined && dumbbellMatch.above === undefined && (
                      <p className="plate-calc-popover-hint">No dumbbells marked as available below.</p>
                    )}
                  </>
                )}

                <div className="plate-calc-sheet-bars">
                  <span className="plate-calc-sheet-label">Dumbbells you have (kg)</span>
                  <div className="plate-calc-chip-row scroll">
                    {DUMBBELL_STEPS_KG.map((dumbbell) => (
                      <button
                        key={dumbbell}
                        type="button"
                        className={availableDumbbells.includes(dumbbell) ? 'plate-calc-chip active' : 'plate-calc-chip'}
                        onClick={() => toggleDumbbellAvailable(dumbbell)}
                      >
                        {dumbbell}
                      </button>
                    ))}
                  </div>
                </div>
              </>
            )}

            {equipmentType === 'added' && (
              <>
                {!hasTarget && (
                  <p className="plate-calc-popover-hint">Enter the target weight on the set to see what to load.</p>
                )}
                {belowBase && <p className="error">Below the machine's own frame weight ({addedBaseKg}kg).</p>}
                {hasTarget && !belowBase && addedLoad === 0 && (
                  <p className="plate-calc-popover-hint">Bodyweight only — nothing to load.</p>
                )}
                {addedResult && (
                  <>
                    <BarDiagram breakdown={addedResult.breakdown} maxPlate={maxPlate} barWeightKg={addedBaseKg} />
                    <p className="plate-calc-sheet-perside">{roundDisplay(addedLoad)}kg to load</p>
                    <PlateDeltaNote delta={addedPlateDelta} />
                    {addedResult.breakdown.length > 0 && (
                      <ul className="plate-calc-breakdown">
                        {addedResult.breakdown.map(({ plate, count }) => (
                          <li key={plate}>
                            <span className="plate-calc-plate">{plate}kg</span>
                            <span>× {count}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                    {addedResult.remainder > 0 && (
                      <p className="error">Can't hit that exactly with your available plates — {addedResult.remainder}kg short.</p>
                    )}
                    <ExercisePlanNote plan={exercisePlan} />
                  </>
                )}

                <div className="plate-calc-sheet-bars">
                  <span className="plate-calc-sheet-label">Base weight (frame, if any)</span>
                  <div className="plate-calc-chip-row">
                    {savedEquipment && (
                      <button
                        type="button"
                        className={selection === SAVED_BAR ? 'plate-calc-chip active' : 'plate-calc-chip'}
                        onClick={selectSaved}
                      >
                        {savedEquipment.name || `This machine (${savedEquipment.kg}kg)`}
                      </button>
                    )}
                    <button
                      type="button"
                      className={selection === CUSTOM_BAR ? 'plate-calc-chip active' : 'plate-calc-chip'}
                      onClick={openCustomForm}
                    >
                      {savedEquipment ? 'Edit' : 'Set base weight'}
                    </button>
                  </div>
                  {selection === CUSTOM_BAR && (
                    <div className="plate-calc-custom-form">
                      <input
                        type="text"
                        placeholder={`Name (optional), e.g. ${exerciseName ? `${exerciseName} frame` : 'Machine frame'}`}
                        value={customName}
                        onChange={(e) => setCustomName(e.target.value)}
                      />
                      <input
                        type="text"
                        inputMode="decimal"
                        placeholder="Frame weight (kg)"
                        value={customWeight}
                        onChange={handleCustomWeightChange}
                        autoFocus
                      />
                      <div className="plate-calc-custom-actions">
                        <button type="button" onClick={handleSaveCustom}>
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

                <PlatesYouHave available={availablePlates} onToggle={togglePlateAvailable} />
              </>
            )}

            {equipmentType === 'hidden' && (
              <p className="plate-calc-popover-hint">No plate math needed for this exercise.</p>
            )}

            <div className="plate-calc-sheet-bars">
              <span className="plate-calc-sheet-label">Calculator type</span>
              <div className="plate-calc-chip-row">
                {Object.entries(EQUIPMENT_TYPE_LABELS).map(([type, label]) => (
                  <button
                    key={type}
                    type="button"
                    className={equipmentType === type ? 'plate-calc-chip active' : 'plate-calc-chip'}
                    onClick={() => selectEquipmentType(type)}
                  >
                    {label}
                  </button>
                ))}
              </div>
              {hasTypeOverride && (
                <button type="button" className="plate-calc-hide-toggle" onClick={resetEquipmentType}>
                  Reset to automatic
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </>,
    document.body,
  )
}
