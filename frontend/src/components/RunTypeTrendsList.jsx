function formatKm(v) {
  return Number(v).toFixed(1)
}

// Only the metrics that mean something for a given session type come back
// non-null — see RunningMetrics.ShapeFor on the backend. Intervals and
// acceleration work report reps and distance covered at speed; a GPS total
// for those would undercount the shuttle turns and pad the time with rest.
function detail(t) {
  const parts = []
  if (t.workRepCount != null) parts.push(`${t.workRepCount} reps`)
  if (t.highSpeedDistanceKm != null) parts.push(`${formatKm(t.highSpeedDistanceKm)} km at speed`)
  if (t.totalDistanceKm != null) parts.push(`${formatKm(t.totalDistanceKm)} km total`)
  if (t.avgDistanceKm != null) parts.push(`${formatKm(t.avgDistanceKm)} km avg`)
  return parts.join(' \u00b7 ')
}

export default function RunTypeTrendsList({ trends }) {
  return (
    <div className="run-type-trend-list">
      {trends.map((t) => {
        const line = detail(t)
        return (
          <div key={t.runSessionTypeId} className="run-type-trend-item">
            <div className="run-type-trend-name">
              <span>{t.runSessionTypeName}</span>
              {line && <span className="run-type-trend-detail">{line}</span>}
            </div>
            <span className="run-type-trend-count">{t.sessionCount} session{t.sessionCount === 1 ? '' : 's'}</span>
          </div>
        )
      })}
    </div>
  )
}
