import { useState } from 'react'
import { formatDateRange } from '../dateUtils'
import LoadBalanceTrend from './LoadBalanceTrend'
import {
  BALANCE_BARS,
  BALANCE_STATE_LABEL,
  balanceBarGeometry,
  balanceHeadline,
  balanceState,
  buildBalanceSeries,
  latestBalanceRow,
  overallBalanceState,
} from '../loadBalance'

const pct = (fraction) => `${(fraction * 100).toFixed(1)}%`

// Garmin Training Load Balance on /wellness: low aerobic, high aerobic and
// anaerobic load, each drawn against Garmin's target range for the day. Colour
// is never the only channel - every bar prints its state as a word and its
// target range as numbers, and the track has an aria-label.
export default function LoadBalanceTile({ rows, days = 84 }) {
  const [trendKey, setTrendKey] = useState(BALANCE_BARS[0].key)
  const latest = latestBalanceRow(rows)
  if (!latest) return null

  const trendBar = BALANCE_BARS.find((b) => b.key === trendKey)
  const headline = balanceHeadline(latest.balancePhrase)
  const overall = overallBalanceState(latest)

  return (
    <section className="load-balance" aria-label="Training load balance">
      <div className="load-balance-head">
        <h2 className="load-balance-title">Load balance</h2>
        <span className="load-balance-date">{formatDateRange(latest.date, latest.date)}</span>
      </div>
      {headline && (
        <div className="load-balance-verdict">
          <span className="load-balance-verdict-label">Garmin feedback</span>
          <p className="load-balance-headline">
            {overall && <span className={`load-balance-dot is-${overall}`} aria-hidden="true" />}
            {headline}
          </p>
        </div>
      )}

      <div className="load-balance-bars">
        {BALANCE_BARS.map((bar) => {
          const value = latest[bar.value]
          const min = latest[bar.min]
          const max = latest[bar.max]
          const state = balanceState(value, min, max)
          const geometry = balanceBarGeometry(value, min, max)
          if (value == null) return null

          const stateLabel = state ? BALANCE_STATE_LABEL[state] : null
          const target = min != null && max != null ? `${min}–${max}` : null
          const ariaLabel = [
            `${bar.label} ${Math.round(value)}`,
            target && `target ${min} to ${max}`,
            stateLabel && stateLabel.toLowerCase(),
          ]
            .filter(Boolean)
            .join(', ')

          return (
            <div key={bar.key} className="load-balance-bar">
              <div className="load-balance-bar-head">
                <span className="load-balance-bar-label">{bar.label}</span>
                <span className="load-balance-bar-value">
                  {Math.round(value)}
                  {stateLabel && <span className={`load-balance-bar-state is-${state}`}>{stateLabel}</span>}
                </span>
              </div>
              <div className="load-balance-track" role="img" aria-label={ariaLabel}>
                {geometry && (
                  <>
                    <span
                      className="load-balance-zone"
                      style={{ left: pct(geometry.zoneStart), width: pct(geometry.zoneEnd - geometry.zoneStart) }}
                    />
                    <span className={`load-balance-fill is-${state}`} style={{ width: pct(geometry.fill) }} />
                  </>
                )}
              </div>
              {target && <div className="load-balance-target">Target {target}</div>}
            </div>
          )
        })}
      </div>

      <div className="load-balance-trend-head">
        <h3 className="load-balance-trend-title">Over time</h3>
        <div className="training-status-range" role="group" aria-label="Load balance trend">
          {BALANCE_BARS.map((b) => (
            <button
              key={b.key}
              type="button"
              className={`training-status-range-btn${b.key === trendKey ? ' active' : ''}`}
              aria-pressed={b.key === trendKey}
              onClick={() => setTrendKey(b.key)}
            >
              {b.short}
            </button>
          ))}
        </div>
      </div>
      <LoadBalanceTrend series={buildBalanceSeries(rows, trendBar, days)} label={trendBar.label} />
    </section>
  )
}
