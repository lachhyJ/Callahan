// Coarse guess at an Ultimate activity's session type, from the Garmin event
// title alone (stored in Notes) — Lachlan's own naming already encodes the
// type ("Throws- Solo", "Pod", "Club Training"). Running's suggestion uses
// aggregate pace/duration because that signal actually correlates with
// session type; neither exists for Ultimate, so that heuristic doesn't
// transfer here — this is a pure keyword match on the title instead. Wrong
// guesses cost nothing, same as Running's: the picker still requires a
// confirming tap.
//
// Checked in this order because a title like "Throws- Solo" should read as
// a Throws session, not a Solo one — "throws" is checked before the more
// generic "solo".
const TITLE_KEYWORDS = [
  { keyword: 'throws', name: 'Throws' },
  { keyword: 'solo', name: 'Solo' },
  { keyword: 'pod', name: 'Pod' },
  { keyword: 'club', name: 'Club Training' },
  { keyword: 'game', name: 'Game' },
]

export function suggestUltimateSessionType(activity, sessionTypes) {
  if (activity.activitySessionTypeId) return null
  const title = activity.notes?.toLowerCase() ?? ''
  if (!title) return null

  const match = TITLE_KEYWORDS.find(({ keyword }) => title.includes(keyword))
  if (!match) return null

  return sessionTypes.find((t) => t.name === match.name) ?? null
}
