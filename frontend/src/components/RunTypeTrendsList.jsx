function formatKm(v) {
  return Number(v).toFixed(1)
}

export default function RunTypeTrendsList({ trends }) {
  return (
    <div className="run-type-trend-list">
      {trends.map((t) => (
        <div key={t.runSessionTypeId} className="run-type-trend-item">
          <div className="run-type-trend-name">
            <span>{t.runSessionTypeName}</span>
            <span className="run-type-trend-detail">{formatKm(t.totalDistanceKm)} km total · {formatKm(t.avgDistanceKm)} km avg</span>
          </div>
          <span className="run-type-trend-count">{t.sessionCount} session{t.sessionCount === 1 ? '' : 's'}</span>
        </div>
      ))}
    </div>
  )
}
