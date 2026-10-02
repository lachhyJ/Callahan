import { useState } from 'react'
import { formatDateRange } from '../dateUtils'
import {
  axisTicks,
  buildStatusDays,
  buildStatusRuns,
  formatAcwr,
  latestStatusRow,
  STATUS_BAND_DAYS,
  statusMeta,
} from '../trainingStatus'

// How many dated runs to list under the band before "Show all", newest first -
// enough to read the recent story without a wall of text on a 6-month range.
const LIST_RUNS = 6

// Garmin Training Status over the last `days` days: the current status with its
// load ratio, a colour band (one segment per run, width = days), and the dated
// run list Connect shows. Colour is never the only channel - every run in the
// list carries its name, and the band has a text summary for screen readers.
//
// `compact` is the dashboard card's version: status name and band only, no
// load line, axis or run list - the card links through to the full view.
export default function TrainingStatusBand({ rows, days, compact = false }) {
  const [showAll, setShowAll] = useState(false)

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

  const newestFirst = [...statusRuns].reverse()
  const listed = showAll ? newestFirst : newestFirst.slice(0, LIST_RUNS)
  const summary = newestFirst
    .slice(0, LIST_RUNS)
    .map((r) => `${statusMeta(r.code).label} ${formatDateRange(r.start, r.end)}`)
    .join('; ')

  // The 1px gap between runs keeps neighbouring hues apart, but past the 12-week
  // default a one-day run is under 2px wide and the gap would eat it.
  const gap = days > STATUS_BAND_DAYS ? 0 : 1

  return (
    <section className={`training-status${compact ? ' training-status-compact' : ''}`} aria-label="Training status">
      <div className="training-status-head">
        <span className="training-status-dot" style={{ background: current.color }} aria-hidden="true" />
        <span className="training-status-name">{current.label}</span>
        {currentRun && (
          <span className="training-status-since">since {formatDateRange(currentRun.start, currentRun.start)}</span>
        )}
      </div>
      {acwr && !compact && <p className="training-status-acwr">{acwr}</p>}

      <div
        className="training-status-band"
        style={{ gap }}
        role="img"
        aria-label={`Training status, last ${days} days. Most recent: ${summary}`}
      >
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
      {!compact && (
        <>
          <div className="training-status-axis" aria-hidden="true">
            {axisTicks(statusDays, 4).map((date) => (
              <span key={date}>{formatDateRange(date, date)}</span>
            ))}
          </div>

          <ul className="training-status-list">
            {listed.map((r) => {
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
          {newestFirst.length > LIST_RUNS && (
            <button type="button" className="training-status-more" onClick={() => setShowAll((v) => !v)}>
              {showAll ? 'Show fewer' : `Show all ${newestFirst.length}`}
            </button>
          )}
        </>
      )}
    </section>
  )
}
