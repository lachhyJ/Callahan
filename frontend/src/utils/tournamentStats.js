// Pure aggregation over a set of Ultimate "Game" activities, shared by the
// tournament summary line on the games list and the tournament detail page so
// the two never drift. No fetching here.
//
// Every field metric on an ActivityDto is nullable — a game with no synced GPS
// track has onFieldSeconds / livePlaySeconds / pointsPlayed all null. We sum
// only the games that have them and report gamesWithMetrics alongside
// gameCount, so a partially-tracked weekend can be caveated rather than
// silently reported as a low total.

function num(v) {
  return v == null ? 0 : Number(v)
}

export function hasFieldMetrics(game) {
  return game.onFieldSeconds != null
}

export function summariseGames(games) {
  const withMetrics = games.filter(hasFieldMetrics)

  const totalOnFieldSeconds = withMetrics.reduce((s, g) => s + num(g.onFieldSeconds), 0)
  const totalOffFieldSeconds = withMetrics.reduce((s, g) => s + num(g.offFieldSeconds), 0)
  const totalMixedSeconds = withMetrics.reduce((s, g) => s + num(g.mixedSeconds), 0)
  const totalLiveSeconds = withMetrics.reduce((s, g) => s + num(g.livePlaySeconds), 0)
  // GPS distance covered inside detected points - the live-play counterpart to
  // totalDistanceKm. Null on games synced before the LivePlayDistanceM column.
  const totalLiveDistanceKm = withMetrics.reduce((s, g) => s + num(g.livePlayDistanceKm), 0)
  const totalPoints = withMetrics.reduce((s, g) => s + num(g.pointsPlayed), 0)
  // Whole-activity GPS distance (Garmin's own figure) - not gated on field
  // metrics, and covers warmup/standing as well as play.
  const totalDistanceKm = games.reduce((s, g) => s + num(g.distanceKm), 0)
  const totalTrackedSeconds = totalOnFieldSeconds + totalOffFieldSeconds + totalMixedSeconds

  return {
    gameCount: games.length,
    gamesWithMetrics: withMetrics.length,
    totalPoints,
    totalOnFieldSeconds,
    totalOffFieldSeconds,
    totalMixedSeconds,
    totalLiveSeconds,
    totalLiveDistanceKm,
    totalTrackedSeconds,
    totalDistanceKm,
    avgLiveMinPerPoint: totalPoints ? totalLiveSeconds / 60 / totalPoints : null,
  }
}

// Chronological (ascending) list of { date, games, summary } — one entry per
// distinct game date. Callers pass games in any order.
export function groupByDay(games) {
  const byDate = new Map()
  for (const game of games) {
    if (!byDate.has(game.date)) byDate.set(game.date, [])
    byDate.get(game.date).push(game)
  }
  return [...byDate.entries()]
    .sort(([a], [b]) => (a < b ? -1 : 1))
    .map(([date, gs]) => ({ date, games: gs, summary: summariseGames(gs) }))
}
