import { formatDateRange } from '../dateUtils'
import {
  buildStatusDays,
  buildStatusRuns,
  formatAcwr,
  latestStatusRow,
  statusMeta,
} from '../trainingStatus'

// How many dated runs to list under the band, newest first - enough to read the
// recent story without re-rendering the whole 12 weeks as text (the band has it).
const LIST_RUNS = 6

// Garmin Training Status over the last `days` days: the current status with its
// load ratio, a colour band (one segment per run, width = days), and the dated
// run list Connect shows. Colour is never the only channel - every run in the
// list carries its name, and the band has a text summary for screen readers.
export default function TrainingStatusBand({ rows, days }) {
  const latest = latestStatusRow(rows)
  if (!latest) return null

  const statusDays = buildStatusDays(rows, days)
  const runs = buildStatusRuns(statusDays)
  const statusRuns = runs.filter((r) => r.code != null)
  const current = statusMeta(latest.trainingStatusCode)
  const acwr = formatAcwr(latest)

  // The current run's start comes from our own per-day codes, not Garmin's
  // sinceDate (see buildStatusRuns). It can only be as far back as the window.
  const currentRun = [...statusRuns].reverse().find((r) => r.end === latest.date)

  const summary = statusRuns
    .slice(-LIST_RUNS)
    .reverse()
    .map((r) => `${statusMeta(r.code).label} ${formatDateRange(r.start, r.end)}`)
    .join('; ')

  return (
    <section className="training-status" aria-label="Training status">
      <div className="training-status-head">
        <span className="training-status-dot" style={{ background: current.color }} aria-hidden="true" />
        <span className="training-status-name">{current.label}</span>
        {currentRun && (
          <span className="training-status-since">since {formatDateRange(currentRun.start, currentRun.start)}</span>
        )}
      </div>
      {acwr && <p className="training-status-acwr">{acwr}</p>}

      <div className="training-status-band" role="img" aria-label={`Training status, last ${days} days. ${summary}`}>
        {runs.map((r) => (
          <span
            key={r.start}
            className="training-status-seg"
            style={{
              flexGrow: r.days,
              background: r.code == null ? 'var(--ts-gap)' : statusMeta(r.code).color,
            }}
          />
        ))}
      </div>
      <div className="training-status-axis" aria-hidden="true">
        <span>{formatDateRange(statusDays[0].date, statusDays[0].date)}</span>
        <span>{formatDateRange(statusDays[statusDays.length - 1].date, statusDays[statusDays.length - 1].date)}</span>
      </div>

      <ul className="training-status-list">
        {statusRuns
          .slice(-LIST_RUNS)
          .reverse()
          .map((r) => {
            const meta = statusMeta(r.code)
            return (
              <li key={r.start} className="training-status-row">
                <span className="training-status-dot" style={{ background: meta.color }} aria-hidden="true" />
                <span className="training-status-row-name">{meta.label}</span>
                <span className="training-status-row-dates">{formatDateRange(r.start, r.end)}</span>
              </li>
            )
          })}
      </ul>
    </section>
  )
}
