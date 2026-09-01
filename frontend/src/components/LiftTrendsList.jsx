import { Link } from 'react-router-dom'
import { LIFT_BASIS, basisNote, formatSet } from '../liftSets'

function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

// The headline figure is the percent change on whatever basis the exercise
// supports. Assisted lifts have no percentage (see LiftProgress), so they
// show the load change instead — which for them reads as assistance coming
// off, and improves as it goes negative.
function delta(t) {
  if (t.deltaPercent != null) {
    return {
      text: `${t.deltaPercent > 0 ? '+' : ''}${Number(t.deltaPercent).toFixed(1)}%`,
      up: t.deltaPercent >= 0,
    }
  }
  if (t.basis === LIFT_BASIS.assisted) {
    const assistOff = -Number(t.deltaKg)
    return { text: `${assistOff > 0 ? '-' : '+'}${formatWeight(Math.abs(assistOff))} kg assist`, up: assistOff > 0 }
  }
  return { text: `${t.deltaKg >= 0 ? '+' : ''}${formatWeight(t.deltaKg)} kg`, up: t.deltaKg >= 0 }
}

export default function LiftTrendsList({ trends }) {
  return (
    <div className="lift-trends-list">
      {trends.map((t) => {
        const d = delta(t)
        return (
          <Link key={t.exerciseId} to={`/exercises/${t.exerciseId}`} className="lift-trend-item">
            <div className="lift-trend-name">
              <span>{t.exerciseName}</span>
              <span className="lift-trend-range">
                {formatMonth(t.earliestMonth)} → {formatMonth(t.latestMonth)} · {formatSet(t.latest, t.basis)}{basisNote(t.basis)}
              </span>
            </div>
            <span className={d.up ? 'lift-trend-delta up' : 'lift-trend-delta down'}>{d.text}</span>
          </Link>
        )
      })}
    </div>
  )
}
