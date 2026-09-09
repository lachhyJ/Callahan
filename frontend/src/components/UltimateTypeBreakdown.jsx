// What made up the Ultimate distance over the window, by primary session type.
// Reuses the run-type-trend-* styles (name + detail + count row) rather than
// growing a third identical ruleset — same shape as the Run mix list above it.
const oneDp = (v) => Number(v).toFixed(1)

function aggregateByType(months) {
  const acc = new Map()
  for (const m of months) {
    for (const t of m.byType) {
      const cur = acc.get(t.typeName) ?? { typeName: t.typeName, km: 0, sessions: 0, without: 0 }
      cur.km += t.km
      cur.sessions += t.sessions
      cur.without += t.sessionsWithoutDistance
      acc.set(t.typeName, cur)
    }
  }
  return [...acc.values()].sort(
    (a, b) => b.km - a.km || b.sessions - a.sessions || a.typeName.localeCompare(b.typeName),
  )
}

export default function UltimateTypeBreakdown({ months }) {
  const rows = aggregateByType(months)
  if (rows.length === 0) return null

  return (
    <div className="run-type-trend-list">
      {rows.map((r) => {
        const detail = r.km > 0 ? `${oneDp(r.km)} km` : null
        const withoutNote = r.without > 0 ? `${r.without} without GPS` : null
        const line = [detail, withoutNote].filter(Boolean).join(' · ')
        return (
          <div key={r.typeName} className="run-type-trend-item">
            <div className="run-type-trend-name">
              <span>{r.typeName}</span>
              {line && <span className="run-type-trend-detail">{line}</span>}
            </div>
            <span className="run-type-trend-count">{r.sessions} session{r.sessions === 1 ? '' : 's'}</span>
          </div>
        )
      })}
    </div>
  )
}
