import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getMonthlyReport, markMonthlyReportViewed } from '../api/client'
import { formatDateLong, formatDateMedium } from '../dateUtils'
import { DIRECTION_CLASS, formatMetricValue } from '../wellnessMetrics'
import { reportProgressNote } from '../utils/reportStatus'

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']

const DIRECTION_LABEL = { below: 'below baseline', above: 'above baseline', in_line: 'in line' }

// Session families, in the order they read on the page. Labels are the
// section headings; the backend tags each count row with one of these.
const FAMILIES = [
  { key: 'Gym', label: 'Gym' },
  { key: 'Running', label: 'Running' },
  { key: 'Ultimate', label: 'Ultimate' },
]

// Movers and stalls can each run to ten rows. Two lifts' worth is what's
// actually readable at a glance; the rest sit behind a toggle.
const COLLAPSED_ROWS = 3

function fmt(n, digits = 1) {
  if (n === null || n === undefined) return '—'
  return Number(n).toFixed(digits)
}

// "a, b and c" — used for the PR and zero-set one-liners.
function joinList(parts) {
  if (parts.length <= 1) return parts.join('')
  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`
}

// A list that shows COLLAPSED_ROWS rows plus a "+N more" toggle. Mirrors the
// season-strength legend's affordance so the two read the same.
function CollapsibleList({ items, renderItem, keyOf }) {
  const [expanded, setExpanded] = useState(false)
  const hidden = items.length - COLLAPSED_ROWS
  const shown = expanded ? items : items.slice(0, COLLAPSED_ROWS)

  return (
    <>
      <ul className="report-list">
        {shown.map((item) => <li key={keyOf(item)}>{renderItem(item)}</li>)}
      </ul>
      {hidden > 0 && (
        <button type="button" className="report-more-toggle" onClick={() => setExpanded((v) => !v)}>
          {expanded ? 'Show fewer' : `+${hidden} more`}
        </button>
      )}
    </>
  )
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
  const w = report.wellness

  // Verdict headline is "Label — one-line detail" since the Aug 2026 rework;
  // older locked snapshots carry just the detail sentence.
  const verdictParts = report.headlineVerdict.split(' — ')
  const hasVerdictLabel = verdictParts.length > 1
  const verdictLabel = hasVerdictLabel ? verdictParts[0] : report.headlineVerdict
  const verdictDetail = hasVerdictLabel ? verdictParts.slice(1).join(' — ') : null

  const prLine = l.prs.length === 0
    ? 'No new bests this month.'
    : `${l.prs.length} new best${l.prs.length === 1 ? '' : 's'}: ${joinList(l.prs.map((p) => `${p.exerciseName} ${fmt(p.e1Rm)} kg`))} e1RM.`

  const zeroSet = l.zeroSetProgramExercises
  const zeroSetLine = zeroSet.length === 0
    ? 'Every program exercise got logged this month.'
    : zeroSet.length <= 3
      ? `Didn't log ${joinList(zeroSet)} from the program this month.`
      : `Didn't log ${zeroSet.slice(0, 2).join(', ')} and ${zeroSet.length - 2} others from the program this month.`

  return (
    <main className="page">
      <h1>{MONTH_NAMES[report.month - 1]} {report.year}</h1>
      {!report.isLocked && (
        <p className="report-settling-note">{reportProgressNote(report.year, report.month)}</p>
      )}

      <section className="report-section">
        <h2>{verdictLabel}</h2>
        {verdictDetail && <p>{verdictDetail}</p>}
      </section>

      <section className="report-section">
        <h3>Consistency</h3>
        <p>{c.totalSessions} sessions across {fmt(c.weeksInMonth)} weeks — {fmt(c.sessionsPerWeek)}/wk vs {fmt(c.trailingSessionsPerWeek)}/wk trailing 3-month average.</p>
        <p>{c.daysTrained} of {c.daysInMonth} days trained.</p>
        {FAMILIES.map(({ key, label }) => {
          const rows = c.sessionsByType.filter((t) => t.family === key)
          if (rows.length === 0) return null
          return (
            <div key={key}>
              <h4>{label}</h4>
              <ul className="report-list">
                {rows.map((t) => <li key={t.label}>{t.label}: {t.count}</li>)}
              </ul>
            </div>
          )
        })}
        <h4>Weekly targets</h4>
        <p className="report-coverage-note">Gym and runs only — Ultimate sessions aren't counted toward these.</p>
        <ul className="report-list">
          {c.weeklyTargets.map((t) => <li key={t.type}>{t.label}: {t.weeksHit}/{t.weeksTotal} weeks</li>)}
        </ul>
      </section>

      <section className="report-section">
        <h3>Load &amp; progression</h3>
        <p className="report-coverage-note">
          Movement is measured across each lift's last {l.windowSessions} logged sessions, which may reach back before this month.
        </p>

        <h4>Movers</h4>
        {l.movers.length === 0 ? <p className="report-empty">Nothing moving meaningfully this month.</p> : (
          <CollapsibleList
            items={l.movers}
            keyOf={(m) => m.exerciseId}
            renderItem={(m) => (
              <>{m.exerciseName}: {m.deltaPercent > 0 ? '+' : ''}{fmt(m.deltaPercent)}% ({fmt(m.fromE1Rm)} → {fmt(m.toE1Rm)} kg e1RM, last {formatDateMedium(m.lastSessionDate)})</>
            )}
          />
        )}

        <h4>Stalls</h4>
        {l.stalls.length === 0 ? <p className="report-empty">No stalls flagged.</p> : (
          <CollapsibleList
            items={l.stalls}
            keyOf={(s) => s.exerciseId}
            renderItem={(s) => (
              <>{s.exerciseName}: flat across last {s.sessionsFlat} sessions (last: {formatDateMedium(s.lastSessionDate)})</>
            )}
          />
        )}

        <p>{prLine}</p>
        <p>{zeroSetLine}</p>
        {report.balance.flaggedLine && (
          <p className="report-balance-line">{report.balance.flaggedLine}</p>
        )}
      </section>

      <section className="report-section">
        <h3>Running &amp; context</h3>

        <h4>Running</h4>
        {report.running.byType.length === 0 ? <p className="report-empty">No runs logged this month.</p> : (
          <ul className="report-list">
            {report.running.byType.map((r) => {
              // Only the metrics that mean something for this session type
              // come back non-null — see RunningMetrics on the backend.
              const parts = [`${r.count} session${r.count === 1 ? '' : 's'}`]
              if (r.workRepCount != null) parts.push(`${r.workRepCount} reps`)
              if (r.highSpeedDistanceKm != null) parts.push(`${fmt(r.highSpeedDistanceKm, 2)} km at speed`)
              if (r.totalDistanceKm != null) parts.push(`${fmt(r.totalDistanceKm)} km`)
              if (r.totalDurationSeconds != null) parts.push(`${Math.round(r.totalDurationSeconds / 60)} min`)
              return <li key={r.typeName}>{r.typeName}: {parts.join(', ')}</li>
            })}
          </ul>
        )}

        <h4>Context</h4>
        {report.context.tournaments.length === 0 && report.context.longestGapDays == null ? (
          <p className="report-empty">Nothing notable framing the month.</p>
        ) : (
          <>
            {report.context.tournaments.length > 0 && (
              <p>Tournaments: {report.context.tournaments.join(', ')}</p>
            )}
            {report.context.longestGapDays != null && (
              <p>Longest gap: {report.context.longestGapDays} days ({formatDateMedium(report.context.longestGapStart)} – {formatDateMedium(report.context.longestGapEnd)})</p>
            )}
          </>
        )}

        {report.taperOverlaps.length > 0 && (
          <>
            <h4>Taper</h4>
            {report.taperOverlaps.map((t) => (
              <div key={t.eventName + t.eventDate} className="report-taper-block">
                <p><strong>{t.eventName}</strong> ({formatDateLong(t.eventDate)}) — {t.overlap} overlap with this month</p>
                <p>Sessions/wk: {fmt(t.rawSessionsPerWeek, 2)} raw, {fmt(t.exclTaperWeeksSessionsPerWeek, 2)} excl. taper weeks</p>
                <p>Planned volume reduction: {t.plannedReductionPercent != null ? `${fmt(t.plannedReductionPercent)}%` : '—'} · Actual: {t.actualReductionPercent != null ? `${fmt(t.actualReductionPercent)}%` : '—'}</p>
                <p>Check-ins completed: {t.checkInsCompleted}/{t.checkInsExpected}</p>
              </div>
            ))}
          </>
        )}
      </section>

      {w && (
        <section className="report-section">
          <h3>Recovery</h3>
          <p className="report-coverage-note">{w.nightsLogged}/{w.daysInMonth} nights logged · {w.nightsUnder7h} under 7h</p>
          <ul className="report-list">
            {w.metrics.map((m) => (
              <li key={m.key}>
                {m.label}: {m.monthAvg == null ? '—' : formatMetricValue(m.key, m.monthAvg)}
                {m.monthAvg != null && m.trailingAvg != null && m.direction !== 'insufficient' && (
                  <> (was {formatMetricValue(m.key, m.trailingAvg)}, <span className={`report-trend ${DIRECTION_CLASS[m.direction] ?? ''}`}>{DIRECTION_LABEL[m.direction] ?? m.direction}</span>)</>
                )}
              </li>
            ))}
          </ul>
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
