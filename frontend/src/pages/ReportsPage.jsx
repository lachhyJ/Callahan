import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMonthlyReports } from '../api/client'
import { ChevronRightIcon } from '../icons'
import { reportProgressTag } from '../utils/reportStatus'
import { MONTH_NAMES } from '../utils/format'


export default function ReportsPage() {
  const [reports, setReports] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getMonthlyReports().then(setReports).catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      <h1>Monthly Reports</h1>

      {error && <p className="error">{error}</p>}
      {!error && reports === null && <p>Loading reports…</p>}

      {reports && reports.length === 0 && (
        <div className="empty-state">
          <p>No reports yet — this fills in after your first month of training.</p>
        </div>
      )}

      {reports && reports.length > 0 && (
        <div className="streak-list">
          {reports.map((r) => (
            <Link key={`${r.year}-${r.month}`} to={`/reports/${r.year}/${r.month}`} className="streak-card">
              <div>
                <span className="streak-label">
                  {MONTH_NAMES[r.month - 1]} {r.year}
                  {!r.isLocked && <span className="report-provisional-tag"> · {reportProgressTag(r.year, r.month)}</span>}
                  {!r.viewed && <span className="report-unviewed-dot" aria-label="Unviewed" />}
                </span>
                <p className="report-headline-preview">{r.headlineVerdict}</p>
              </div>
              <div className="streak-numbers">
                <ChevronRightIcon />
              </div>
            </Link>
          ))}
        </div>
      )}
    </main>
  )
}
