import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getMonthlyReport, markMonthlyReportViewed } from '../api/client'
import { formatDateLong, formatDateMedium } from '../dateUtils'

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

function fmt(n, digits = 1) {
  if (n === null || n === undefined) return '—'
  return Number(n).toFixed(digits)
}

export default function ReportDetailPage() {
  const { year, month } = useParams()
  const [report, setReport] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    setReport(null)
    setError(null)
    getMonthlyReport(year, month).then((r) => {
      setReport(r)
      if (!r.viewedAt) markMonthlyReportViewed(year, month).catch(() => {})
    }).catch((err) => setError(err.message))
  }, [year, month])

  if (error) return <main className="page"><p className="error">{error}</p></main>
  if (!report) return <main className="page"><p>Loading report…</p></main>

  const c = report.consistency
  const l = report.loadProgression

  return (
    <main className="page">
      <h1>{MONTH_NAMES[report.month - 1]} {report.year}</h1>
      {!report.isLocked && (
        <p className="report-settling-note">Still settling — this report will lock on day 8 of next month.</p>
      )}

      <section className="report-section">
        <h2>{report.headlineVerdict}</h2>
      </section>

      <section className="report-section">
        <h3>Consistency</h3>
        <p>{c.totalSessions} sessions across {fmt(c.weeksInMonth)} weeks — {fmt(c.sessionsPerWeek)}/wk vs {fmt(c.trailingSessionsPerWeek)}/wk trailing 3-month average.</p>
        <p>{c.daysTrained} of {c.daysInMonth} days trained.</p>
        <ul className="report-list">
          {c.sessionsByType.map((t) => <li key={t.label}>{t.label}: {t.count}</li>)}
        </ul>
        <h4>Weekly targets</h4>
        <ul className="report-list">
          {c.weeklyTargets.map((w) => <li key={w.type}>{w.label}: {w.weeksHit}/{w.weeksTotal} weeks</li>)}
        </ul>
      </section>

      <section className="report-section">
        <h3>Load & progression</h3>
        <h4>PRs (best e1RM)</h4>
        {l.prs.length === 0 ? <p className="report-empty">No new PRs this month.</p> : (
          <ul className="report-list">
            {l.prs.map((p) => (
              <li key={p.exerciseId}>
                {p.exerciseName}: {fmt(p.e1Rm)} kg e1RM{p.previousE1Rm != null ? `, up from ${fmt(p.previousE1Rm)} kg` : ''} ({formatDateMedium(p.date)})
              </li>
            ))}
          </ul>
        )}
        <h4>Movers</h4>
        {l.movers.length === 0 ? <p className="report-empty">Nothing moving meaningfully this month.</p> : (
          <ul className="report-list">
            {l.movers.map((m) => <li key={m.exerciseId}>{m.exerciseName}: {m.deltaPercent > 0 ? '+' : ''}{fmt(m.deltaPercent)}% ({fmt(m.fromE1Rm)} → {fmt(m.toE1Rm)} kg e1RM)</li>)}
          </ul>
        )}
        <h4>Stalls</h4>
        {l.stalls.length === 0 ? <p className="report-empty">No stalls flagged.</p> : (
          <ul className="report-list">
            {l.stalls.map((s) => <li key={s.exerciseId}>{s.exerciseName}: flat across last {s.sessionsFlat} sessions (last: {formatDateMedium(s.lastSessionDate)})</li>)}
          </ul>
        )}
        <h4>Zero-set program exercises</h4>
        {l.zeroSetProgramExercises.length === 0 ? <p className="report-empty">Every program exercise got logged this month.</p> : (
          <ul className="report-list">
            {l.zeroSetProgramExercises.map((name) => <li key={name}>{name}</li>)}
          </ul>
        )}
      </section>

      <section className="report-section">
        <h3>Running</h3>
        {report.running.byType.length === 0 ? <p className="report-empty">No runs logged this month.</p> : (
          <ul className="report-list">
            {report.running.byType.map((r) => (
              <li key={r.typeName}>{r.typeName}: {r.count} sessions, {fmt(r.totalDistanceKm)} km, {Math.round(r.totalDurationSeconds / 60)} min</li>
            ))}
          </ul>
        )}
      </section>

      <section className="report-section">
        <h3>Balance</h3>
        <p className={report.balance.flaggedLine ? undefined : 'report-empty'}>{report.balance.flaggedLine ?? 'No push/pull imbalance flagged this month.'}</p>
      </section>

      <section className="report-section">
        <h3>Context</h3>
        {report.context.tournaments.length > 0 && (
          <p>Tournaments: {report.context.tournaments.join(', ')}</p>
        )}
        {report.context.longestGapDays != null && (
          <p>Longest gap: {report.context.longestGapDays} days ({formatDateMedium(report.context.longestGapStart)} – {formatDateMedium(report.context.longestGapEnd)})</p>
        )}
      </section>

      {report.taperOverlaps.length > 0 && (
        <section className="report-section">
          <h3>Taper</h3>
          {report.taperOverlaps.map((t) => (
            <div key={t.eventName + t.eventDate} className="report-taper-block">
              <p><strong>{t.eventName}</strong> ({formatDateLong(t.eventDate)}) — {t.overlap} overlap with this month</p>
              <p>Sessions/wk: {fmt(t.rawSessionsPerWeek, 2)} raw, {fmt(t.exclTaperWeeksSessionsPerWeek, 2)} excl. taper weeks</p>
              <p>Planned volume reduction: {t.plannedReductionPercent != null ? `${fmt(t.plannedReductionPercent)}%` : '—'} · Actual: {t.actualReductionPercent != null ? `${fmt(t.actualReductionPercent)}%` : '—'}</p>
              <p>Check-ins completed: {t.checkInsCompleted}/{t.checkInsExpected}</p>
            </div>
          ))}
        </section>
      )}

      <section className="report-section">
        <h3>Questions for next month</h3>
        {report.nextMonthQuestions.length === 0 ? <p className="report-empty">Nothing flagged.</p> : (
          <ul className="report-list">
            {report.nextMonthQuestions.map((q, i) => <li key={i}>{q}</li>)}
          </ul>
        )}
      </section>
    </main>
  )
}
