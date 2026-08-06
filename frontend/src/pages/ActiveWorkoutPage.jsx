import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { cancelRestTimer, createWorkoutSession, getExerciseHistory, getFinishers, scheduleRestTimer, startWorkoutTemplate } from '../api/client'
import { clearActiveWorkout, loadActiveWorkout, saveActiveWorkout } from '../activeWorkout'
import { enableRestAlerts, hasActiveSubscription, pushSupported } from '../push'
import { BellIcon, CheckIcon } from '../icons'

const SET_TYPE_LABELS = { Warmup: 'W', Normal: '', Failure: 'F', Drop: 'D' }
const SET_TYPE_OPTIONS = ['Warmup', 'Normal', 'Failure', 'Drop']
const REST_PRESETS = [60, 90, 120, 150, 180]

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function formatDuration(ms) {
  const totalSeconds = Math.floor(ms / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

const KG_PER_LB = 0.45359237

function lbToKgRoundedDown(lb) {
  return Math.floor(lb * KG_PER_LB * 2) / 2
}

function kgToLbDisplay(weightKg) {
  if (weightKg === '' || weightKg === null || weightKg === undefined) return ''
  return String(Math.round((Number(weightKg) / KG_PER_LB) * 10) / 10)
}

function playBeep() {
  const AudioContextClass = window.AudioContext || window.webkitAudioContext
  if (!AudioContextClass) return
  const ctx = new AudioContextClass()
  for (const startDelay of [0, 0.3]) {
    const oscillator = ctx.createOscillator()
    const gain = ctx.createGain()
    oscillator.type = 'sine'
    oscillator.frequency.value = 880
    const startTime = ctx.currentTime + startDelay
    gain.gain.setValueAtTime(0.0001, startTime)
    gain.gain.exponentialRampToValueAtTime(0.3, startTime + 0.01)
    gain.gain.exponentialRampToValueAtTime(0.0001, startTime + 0.25)
    oscillator.connect(gain)
    gain.connect(ctx.destination)
    oscillator.start(startTime)
    oscillator.stop(startTime + 0.25)
  }
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
    restSeconds: ex.restSeconds,
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
  const [restTimer, setRestTimer] = useState(null)
  const [pushEnabled, setPushEnabled] = useState(false)
  const [pushError, setPushError] = useState(null)
  const [lbInputs, setLbInputs] = useState({})
  const [focusedWeightCell, setFocusedWeightCell] = useState(null)
  const [focusedRestExIdx, setFocusedRestExIdx] = useState(null)
  const [showMiniBar, setShowMiniBar] = useState(false)
  const [historyExercise, setHistoryExercise] = useState(null)
  const [historyData, setHistoryData] = useState(null)
  const [historyError, setHistoryError] = useState(null)
  const navigate = useNavigate()
  const hasAutoScrolled = useRef(false)
  const headerRef = useRef(null)

  useEffect(() => {
    // Mini bar only takes over once the real header has actually scrolled
    // out of view — otherwise both show "Finish" at once right at the top.
    function handleScroll() {
      if (!headerRef.current) return
      setShowMiniBar(headerRef.current.getBoundingClientRect().bottom < 0)
    }
    handleScroll()
    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [exercises])

  useEffect(() => {
    if (!historyExercise) return
    setHistoryData(null)
    setHistoryError(null)
    getExerciseHistory(historyExercise.exerciseId)
      .then(setHistoryData)
      .catch((err) => setHistoryError(err.message))
  }, [historyExercise])

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
    hasActiveSubscription().then(setPushEnabled).catch(() => {})
  }, [templateId])

  useEffect(() => {
    // Once only, on arrival — resuming a partially-done workout should land
    // you where you left off, not force a manual scroll-hunt. Must NOT
    // re-fire on every tick or it'd yank the screen away mid-workout
    // whenever you're deliberately scrolling elsewhere (checking another
    // exercise, adding a note).
    if (!exercises || hasAutoScrolled.current) return
    hasAutoScrolled.current = true
    for (let exIdx = 0; exIdx < exercises.length; exIdx++) {
      const setIdx = exercises[exIdx].sets.findIndex((s) => !s.completed)
      if (setIdx === -1) continue
      const row = document.getElementById(`set-${exIdx}-${setIdx}`)
      if (row) {
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
        row.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'center' })
      }
      break
    }
  }, [exercises])

  useEffect(() => {
    if (!exercises) return
    saveActiveWorkout({ templateId: Number(templateId), templateName, exercises, startedAt: startedAt.toISOString() })
  }, [exercises, templateName, templateId, startedAt])

  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 1000)
    // Backgrounded/locked tabs get their setInterval throttled by the OS, so
    // the 1s ticks alone drift. Force an immediate resync the moment the tab
    // becomes visible again rather than waiting for the next (possibly still
    // throttled) tick.
    const onVisible = () => {
      if (document.visibilityState === 'visible') setNow(new Date())
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [])

  useEffect(() => {
    if (!restTimer) return
    // Derived from an absolute end timestamp, not decremented per tick — so
    // whenever `now` catches up (even after a long throttled gap), the
    // remaining time is always correct rather than having drifted.
    const remaining = Math.round((restTimer.endAt - now.getTime()) / 1000)
    if (remaining <= 0) {
      playBeep()
      setRestTimer(null)
    }
  }, [now, restTimer])

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

  function toggleLbMode(exIdx, setIdx, weightKg) {
    const key = `${exIdx}-${setIdx}`
    setLbInputs((prev) => {
      const next = { ...prev }
      if (key in next) {
        delete next[key]
      } else {
        next[key] = kgToLbDisplay(weightKg)
      }
      return next
    })
  }

  function updateLbInput(exIdx, setIdx, value) {
    const key = `${exIdx}-${setIdx}`
    setLbInputs((prev) => ({ ...prev, [key]: value }))
    const lbNum = Number(value)
    const weightKg = value === '' || Number.isNaN(lbNum) ? '' : String(lbToKgRoundedDown(lbNum))
    updateSet(exIdx, setIdx, 'weightKg', weightKg)
  }

  function handleWeightBlur(cellKey, exIdx, setIdx) {
    setTimeout(() => {
      setFocusedWeightCell((prev) => (prev === cellKey ? null : prev))
      // Revert display back to kg on blur. The kg value was already kept in
      // sync on every keystroke (see updateLbInput) — this only clears which
      // unit the field *shows*, so clicking away doesn't leave the lb text
      // sitting there unconverted-looking.
      setLbInputs((prev) => {
        if (!(cellKey in prev)) return prev
        const next = { ...prev }
        delete next[cellKey]
        return next
      })
      // Fill weight into other still-blank working sets from the first
      // working set only — never overwrites a value you've already typed,
      // never touches Warmup, never touches an already-ticked set.
      setExercises((prev) =>
        prev.map((ex, i) => {
          if (i !== exIdx) return ex
          const firstWorkingIdx = ex.sets.findIndex((s) => s.type !== 'Warmup')
          if (setIdx !== firstWorkingIdx) return ex
          const value = ex.sets[setIdx].weightKg
          if (value === '') return ex
          return {
            ...ex,
            sets: ex.sets.map((s, j) =>
              j === setIdx || s.type === 'Warmup' || s.completed || s.weightKg !== ''
                ? s
                : { ...s, weightKg: value }
            ),
          }
        })
      )
    }, 150)
  }

  function updateNotes(exIdx, value) {
    setExercises((prev) => prev.map((ex, i) => (i !== exIdx ? ex : { ...ex, notes: value })))
  }

  function updateExerciseRest(exIdx, value) {
    const restSeconds = value === '' ? '' : Math.max(0, Number(value))
    setExercises((prev) => prev.map((ex, i) => (i !== exIdx ? ex : { ...ex, restSeconds })))
  }

  function handleRestBlur(exIdx) {
    setTimeout(() => {
      setFocusedRestExIdx((prev) => (prev === exIdx ? null : prev))
    }, 150)
  }

  async function startRestTimer(exercise, nextSetNumber) {
    if (restTimer?.timerId) {
      cancelRestTimer(restTimer.timerId).catch(() => {})
    }
    const duration = exercise.restSeconds || 90
    setRestTimer({
      endAt: Date.now() + duration * 1000,
      totalSeconds: duration,
      timerId: null,
      exerciseName: exercise.exerciseName,
      targetReps: exercise.targetReps,
      nextSetNumber,
      totalSets: exercise.sets.length,
    })
    try {
      const { timerId } = await scheduleRestTimer(duration, exercise.exerciseName, exercise.targetReps, nextSetNumber, exercise.sets.length)
      setRestTimer((prev) => (prev ? { ...prev, timerId } : prev))
    } catch {
      // Local countdown still works even if the backend push couldn't be scheduled.
    }
  }

  function toggleComplete(exIdx, setIdx) {
    const exercise = exercises[exIdx]
    const set = exercise.sets[setIdx]
    if (!set.completed && set.reps === '') {
      setError('Enter reps before marking a set complete.')
      return
    }
    setError(null)
    const nowCompleting = !set.completed
    setExercises((prev) =>
      prev.map((ex, i) =>
        i !== exIdx
          ? ex
          : { ...ex, sets: ex.sets.map((s, j) => (j !== setIdx ? s : { ...s, completed: !s.completed })) }
      )
    )
    if (nowCompleting) startRestTimer(exercise, set.setOrder + 1)
  }

  function adjustRest(deltaSeconds) {
    setRestTimer((prev) => {
      if (!prev) return prev
      const newEndAt = Math.max(Date.now(), prev.endAt + deltaSeconds * 1000)
      if (prev.timerId) {
        const newRemaining = Math.max(0, Math.round((newEndAt - Date.now()) / 1000))
        cancelRestTimer(prev.timerId).catch(() => {})
        scheduleRestTimer(newRemaining, prev.exerciseName, prev.targetReps, prev.nextSetNumber, prev.totalSets)
          .then(({ timerId }) => setRestTimer((cur) => (cur ? { ...cur, timerId } : cur)))
          .catch(() => {})
      }
      return { ...prev, endAt: newEndAt, timerId: null }
    })
  }

  function skipRest() {
    if (restTimer?.timerId) {
      cancelRestTimer(restTimer.timerId).catch(() => {})
    }
    setRestTimer(null)
  }

  async function handleEnableAlerts() {
    setPushError(null)
    try {
      await enableRestAlerts()
      setPushEnabled(true)
    } catch (err) {
      setPushError(err.message)
    }
  }

  function setType(exIdx, setIdx, type) {
    updateSet(exIdx, setIdx, 'type', type)
    setOpenTypeMenu(null)
  }

  function removeSet(exIdx, setIdx) {
    const ex = exercises[exIdx]
    const set = ex.sets[setIdx]
    const warning = set.completed ? ' This set is already marked complete.' : ''
    if (!window.confirm(`Remove set ${set.setOrder} of ${ex.exerciseName}?${warning}`)) return
    setExercises((prev) => {
      const target = prev[exIdx]
      const remainingSets = target.sets.filter((_, j) => j !== setIdx).map((s, j) => ({ ...s, setOrder: j + 1 }))
      if (remainingSets.length === 0) {
        return prev.filter((_, i) => i !== exIdx)
      }
      return prev.map((e, i) => (i !== exIdx ? e : { ...e, sets: remainingSets }))
    })
    setOpenTypeMenu(null)
  }

  function removeExercise(exIdx) {
    const ex = exercises[exIdx]
    const completedCount = completedSetsFor(ex).length
    const warning = completedCount > 0 ? ` This removes ${completedCount} already-completed set${completedCount === 1 ? '' : 's'}.` : ''
    if (!window.confirm(`Remove ${ex.exerciseName}?${warning}`)) return
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
    if (restTimer?.timerId) cancelRestTimer(restTimer.timerId).catch(() => {})
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
      navigate('/history', { state: { savedMessage: 'Workout saved' } })
    } catch (err) {
      setError(err.message)
      setSaving(false)
    }
  }

  function handleDiscard() {
    if (restTimer?.timerId) cancelRestTimer(restTimer.timerId).catch(() => {})
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
      <div className={showMiniBar ? 'mini-progress-bar visible' : 'mini-progress-bar'}>
        <span>{formatDuration(now - startedAt)}</span>
        <button type="button" onClick={() => setShowSummary(true)}>Finish</button>
      </div>
      <div className="active-workout-header" ref={headerRef}>
        <h1>{templateName}</h1>
        <button type="button" onClick={() => setShowSummary(true)}>Finish</button>
      </div>
      <div className="live-stats">
        <span>{formatDuration(now - startedAt)}</span>
        <span>{stats.volume.toLocaleString()} kg</span>
        <span>{stats.setCount} set{stats.setCount === 1 ? '' : 's'}</span>
      </div>
      {error && <p className="error">{error}</p>}

      {!pushEnabled && pushSupported() && (
        <div className="push-prompt">
          <span><BellIcon /> Get a rest-timer alert even if your phone locks</span>
          <button type="button" className="secondary-btn" onClick={handleEnableAlerts}>Enable rest alerts</button>
          {pushError && <p className="error">{pushError}</p>}
        </div>
      )}

      {exercises.map((ex, exIdx) => {
        const isResting = restTimer?.exerciseName === ex.exerciseName
        return (
        <div key={`${ex.exerciseId}-${exIdx}`} className="exercise-card">
          <div className="exercise-card-header">
            <div className="exercise-card-title">
              <h2>
                <button
                  type="button"
                  className="exercise-name-btn"
                  onClick={() => setHistoryExercise({ exerciseId: ex.exerciseId, exerciseName: ex.exerciseName })}
                >
                  {ex.exerciseName}
                </button>
              </h2>
              {isResting && (
                <span className="resting-badge">
                  <span className="resting-dot" />
                  Resting
                </span>
              )}
            </div>
            <button type="button" className="remove-exercise-btn" onClick={() => removeExercise(exIdx)} aria-label={`Remove ${ex.exerciseName}`}>
              Remove
            </button>
          </div>
          <p className="target-reps">
            Target: {ex.sets.length} × {ex.targetReps} · rest{' '}
            <input
              type="number"
              inputMode="numeric"
              pattern="[0-9]*"
              min="0"
              step="15"
              className="rest-input"
              style={{ width: `${Math.max(String(ex.restSeconds).length, 1) + 1}ch` }}
              value={ex.restSeconds}
              onChange={(e) => updateExerciseRest(exIdx, e.target.value)}
              onFocus={(e) => {
                setFocusedRestExIdx(exIdx)
                e.target.select()
              }}
              onBlur={() => handleRestBlur(exIdx)}
              aria-label={`Rest time for ${ex.exerciseName}`}
            />
            s
          </p>
          {focusedRestExIdx === exIdx && (
            <div className="rest-presets">
              {REST_PRESETS.map((preset) => (
                <button
                  key={preset}
                  type="button"
                  className={ex.restSeconds === preset ? 'rest-preset active' : 'rest-preset'}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => updateExerciseRest(exIdx, preset)}
                >
                  {preset}s
                </button>
              ))}
            </div>
          )}
          <input
            type="text"
            placeholder="Add notes here…"
            className="exercise-notes-input"
            value={ex.notes}
            onChange={(e) => updateNotes(exIdx, e.target.value)}
          />
          <div className="table-scroll">
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
              {(() => {
                let workingSetNumber = 0
                return ex.sets.map((s, setIdx) => {
                  if (s.type !== 'Warmup') workingSetNumber += 1
                  return (
                <tr id={`set-${exIdx}-${setIdx}`} key={setIdx} className={s.completed ? 'set-row completed' : 'set-row'}>
                  <td className="set-number-cell">
                    <button
                      type="button"
                      className={`set-number set-type-${s.type.toLowerCase()}`}
                      onClick={() => setOpenTypeMenu(openTypeMenu?.exIdx === exIdx && openTypeMenu?.setIdx === setIdx ? null : { exIdx, setIdx })}
                    >
                      {SET_TYPE_LABELS[s.type] || workingSetNumber}
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
                    {(() => {
                      const cellKey = `${exIdx}-${setIdx}`
                      const isLb = cellKey in lbInputs
                      const isFocused = focusedWeightCell === cellKey
                      return (
                        <div className="weight-input-group">
                          <input
                            type="number"
                            step={isLb ? '0.1' : '0.5'}
                            placeholder="0"
                            value={isLb ? lbInputs[cellKey] : s.weightKg}
                            onChange={(e) =>
                              isLb
                                ? updateLbInput(exIdx, setIdx, e.target.value)
                                : updateSet(exIdx, setIdx, 'weightKg', e.target.value)
                            }
                            onFocus={(e) => {
                              setFocusedWeightCell(cellKey)
                              e.target.select()
                            }}
                            onBlur={() => handleWeightBlur(cellKey, exIdx, setIdx)}
                            className={s.previous && !s.completed ? 'prefilled' : ''}
                          />
                          {isFocused && (
                            <button
                              type="button"
                              className="unit-toggle"
                              onMouseDown={(e) => e.preventDefault()}
                              onClick={() => toggleLbMode(exIdx, setIdx, s.weightKg)}
                            >
                              {isLb ? 'lb' : 'kg'}
                            </button>
                          )}
                        </div>
                      )
                    })()}
                  </td>
                  <td>
                    <input
                      type="number"
                      value={s.reps}
                      onChange={(e) => updateSet(exIdx, setIdx, 'reps', e.target.value)}
                      onFocus={(e) => e.target.select()}
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
                      {s.completed ? <CheckIcon /> : null}
                    </button>
                  </td>
                </tr>
                  )
                })
              })()}
            </tbody>
          </table>
          </div>
          <button type="button" className="add-set-btn" onClick={() => addSet(exIdx)}>+ Add set</button>
        </div>
        )
      })}

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

      {restTimer && (() => {
        const remainingSeconds = Math.max(0, Math.round((restTimer.endAt - now.getTime()) / 1000))
        const progress = restTimer.totalSeconds > 0 ? remainingSeconds / restTimer.totalSeconds : 0
        const urgent = progress < 0.15
        return (
          <div className="rest-bar">
            <div
              className={urgent ? 'rest-progress urgent' : 'rest-progress'}
              style={{ transform: `scaleX(${progress})` }}
            />
            <button type="button" onClick={() => adjustRest(-15)}>-15</button>
            <span className="rest-countdown">{formatCountdown(remainingSeconds)}</span>
            <button type="button" onClick={() => adjustRest(15)}>+15</button>
            <button type="button" className="skip-btn" onClick={skipRest}>Skip</button>
          </div>
        )
      })()}

      {historyExercise && (
        <div className="modal-backdrop" onClick={() => setHistoryExercise(null)}>
          <div className="modal history-modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{historyExercise.exerciseName}</h2>
              <button type="button" className="modal-close-btn" onClick={() => setHistoryExercise(null)} aria-label="Close">
                ×
              </button>
            </div>
            <div className="modal-body">
              {historyError && <p className="error">{historyError}</p>}
              {!historyError && historyData === null && <p>Loading…</p>}
              {!historyError && historyData?.length === 0 && <p>No previous sets logged for this exercise yet.</p>}
              {historyData?.map((entry) => (
                <div key={entry.workoutSessionId} className="history-entry">
                  <strong>{entry.date}</strong>
                  <ul className="history-set-list">
                    {entry.sets.map((s) => (
                      <li key={s.setOrder}>
                        <span className="history-set-number">Set {s.setOrder + 1}</span>
                        <span className={`history-set-type set-type-${s.setType.toLowerCase()}`}>{SET_TYPE_LABELS[s.setType]}</span>
                        <span>{s.reps} × {s.weightKg} kg</span>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </main>
  )
}
