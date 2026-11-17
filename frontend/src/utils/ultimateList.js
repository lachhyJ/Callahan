// Ordering and season grouping for the Ultimate page's list. Entries are
// { kind: 'tournament', tournament, games, pending, sortDate } or
// { kind: 'game', game, sortDate }. All dates compare as ISO strings.

// How long a finished tournament keeps the top spot: the week after a
// weekend is when its games get reviewed, and that shouldn't mean scrolling
// past every upcoming tournament already entered.
export const RECENT_DAYS = 7

// 0 = on now or finished within RECENT_DAYS, 1 = upcoming, 2 = everything else.
// recentSince is the ISO date RECENT_DAYS ago.
export function rank(entry, today, recentSince) {
  if (entry.kind !== 'tournament') return 2
  const t = entry.tournament
  if (t.startDate > today) return 1
  return t.endDate >= recentSince ? 0 : 2
}

// Live / just-finished first, then upcoming soonest first, then the rest
// newest first.
export function sortEntries(entries, today, recentSince) {
  return [...entries].sort((a, b) => {
    const ra = rank(a, today, recentSince)
    const rb = rank(b, today, recentSince)
    if (ra !== rb) return ra - rb
    if (ra === 1) return a.tournament.startDate < b.tournament.startDate ? -1 : 1
    return a.sortDate < b.sortDate ? 1 : a.sortDate > b.sortDate ? -1 : 0
  })
}

// The season the page is centred on: the one containing today, else (in the
// off-season gap) the most recently started one. Seasons that haven't started
// yet never qualify - featuring one would sweep everything before it into
// "past" while it's still empty.
export function pickCurrentSeason(seasons, today) {
  const started = seasons
    .filter((s) => s.startDate <= today)
    .sort((a, b) => (a.startDate < b.startDate ? 1 : -1))
  return started.find((s) => today <= s.endDate) ?? started[0] ?? null
}

// A tournament's explicit season link wins; otherwise (and for standalone
// games) it's whichever season's range contains its date.
export function seasonForEntry(entry, seasons) {
  if (entry.kind === 'tournament' && entry.tournament.seasonId != null) {
    return seasons.find((s) => s.id === entry.tournament.seasonId) ?? null
  }
  const date = (entry.kind === 'tournament' ? entry.tournament.startDate : entry.game.date).slice(0, 10)
  return seasons.find((s) => s.startDate <= date && date <= s.endDate) ?? null
}

// Splits sorted entries into the main list and collapsed past-season groups.
// An entry is "past" when it ranks 2 (not live/recent/upcoming) and belongs
// to a season other than the current or a later one - or to no season but
// dates from before the current season started. With no current season,
// nothing is past. Groups come back newest season first, the no-season
// bucket ("Earlier") last.
export function partitionBySeason(sorted, seasons, today, recentSince) {
  const current = pickCurrentSeason(seasons, today)
  if (!current) return { current: null, main: sorted, pastGroups: [] }

  const isPastSeason = (s) => s.id !== current.id && s.endDate < current.startDate
  const main = []
  const groups = new Map()
  for (const entry of sorted) {
    if (rank(entry, today, recentSince) !== 2) {
      main.push(entry)
      continue
    }
    const season = seasonForEntry(entry, seasons)
    const date = entry.sortDate.slice(0, 10)
    const past = season ? isPastSeason(season) : date < current.startDate
    if (!past) {
      main.push(entry)
      continue
    }
    const key = season ? season.id : 'none'
    if (!groups.has(key)) groups.set(key, { season, entries: [] })
    groups.get(key).entries.push(entry)
  }
  // Every past season gets a group even with nothing in it, so its admin
  // (edit / delete) stays reachable.
  for (const s of seasons) {
    if (isPastSeason(s) && !groups.has(s.id)) groups.set(s.id, { season: s, entries: [] })
  }
  const pastGroups = [...groups.values()].sort((a, b) => {
    if (!a.season) return 1
    if (!b.season) return -1
    return a.season.startDate < b.season.startDate ? 1 : -1
  })
  return { current, main, pastGroups }
}

// "6 tournaments · 18 games" for a collapsed past-season header.
export function groupCounts(entries) {
  let tournaments = 0
  let games = 0
  for (const e of entries) {
    if (e.kind === 'tournament') {
      tournaments += 1
      games += e.games.length
    } else {
      games += 1
    }
  }
  return { tournaments, games }
}
