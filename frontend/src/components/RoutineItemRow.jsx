import { useEffect, useState } from 'react'
import { advanceHold } from '../activeWorkout'
import { playBeepNow } from '../audio'

// A single routine item's hold timer, ticking independently of the page. Not
// persisted across a reload/navigation — unlike the rest timer, there's no
// requirement to survive backgrounding here, this is a manual in-app pass.
export default function RoutineItemRow({ item }) {
  const [holdTimer, setHoldTimer] = useState(null)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!holdTimer) return
    const interval = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [holdTimer])

  useEffect(() => {
    if (!holdTimer) return
    const step = advanceHold(holdTimer, { isPerSide: item.isPerSide }, now)
    if (!step) return
    playBeepNow()
    if (step.type === 'advance') {
      setHoldTimer((h) => (h ? { ...h, ...step.holdTimer } : h))
    } else {
      setHoldTimer(null)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [holdTimer, now])

  function start() {
    playBeepNow() // also unlocks audio, same as the workout hold timer
    setHoldTimer({
      endsAt: Date.now() + item.holdSeconds * 1000,
      phase: 'hold',
      side: 1,
      targetSeconds: item.holdSeconds,
      delaySeconds: Math.max(0, Math.round(Number(item.perSideDelaySeconds) || 0)),
    })
  }

  // Jump straight into side 2 instead of waiting out the rest of the gap.
  function skipGap() {
    playBeepNow()
    setHoldTimer((h) => (h ? { ...h, phase: 'hold', side: 2, endsAt: Date.now() + h.targetSeconds * 1000 } : h))
  }

  const remaining = holdTimer ? Math.max(0, Math.round((holdTimer.endsAt - now) / 1000)) : null

  return (
    <div className="routine-item">
      <div className="routine-item-main">
        <span className="routine-item-name">{item.name}</span>
        {item.prescription && <span className="routine-item-prescription">{item.prescription}</span>}
        {item.cue && <span className="routine-item-cue">{item.cue}</span>}
      </div>
      {item.holdSeconds != null && (
        holdTimer ? (
          holdTimer.phase === 'gap' ? (
            <button type="button" className="routine-item-countdown routine-item-skip-gap" onClick={skipGap}>
              Switch sides… ({remaining}s)
            </button>
          ) : (
            <span className="routine-item-countdown" aria-live="polite">
              {`${item.isPerSide ? `Side ${holdTimer.side} — ` : ''}${remaining}s`}
            </span>
          )
        ) : (
          <button type="button" className="routine-item-start" onClick={start}>Start</button>
        )
      )}
    </div>
  )
}
