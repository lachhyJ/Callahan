import { Link } from 'react-router-dom'

function formatWeight(v) {
  return Number(v) % 1 === 0 ? String(v) : Number(v).toFixed(1)
}

function formatMonth(iso) {
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

export default function LiftTrendsList({ trends }) {
  return (
    <div className="lift-trends-list">
      {trends.map((t) => (
        <Link key={t.exerciseId} to={`/exercises/${t.exerciseId}`} className="lift-trend-item">
          <div className="lift-trend-name">
            <span>{t.exerciseName}</span>
            <span className="lift-trend-range">{formatMonth(t.earliestMonth)} → {formatMonth(t.latestMonth)}</span>
          </div>
          <span className={t.deltaKg >= 0 ? 'lift-trend-delta up' : 'lift-trend-delta down'}>
            {t.deltaKg >= 0 ? '+' : ''}{formatWeight(t.deltaKg)} kg
          </span>
        </Link>
      ))}
    </div>
  )
}
