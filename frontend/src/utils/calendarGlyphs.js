// Calendar day-cell glyphs. Colour = family lane (blue for Run/Field, teal for
// Ultimate); shape = the session subtype within that lane. A shared 4-shape
// vocabulary (disc / triangle / diamond / ring) is reused across lanes since
// colour already separates them. Ultimate pairs a filled shape with a hollow
// partner: disc/ring for Throws+Pod vs Solo, filled/hollow triangle for Game vs
// Club Training. Unclassified activities fall back to their lane's misc shape -
// for Ultimate a hollow square, so an untagged day can't pass for a real type.
//
// One glyph per distinct (lane, shape) across EVERY session-type tag on the
// day's activities — so an activity tagged Field 1 + Throws shows a blue
// triangle AND a teal ring, not just its primary.

const RUN_SHAPE_BY_NAME = {
  'High Speed Intervals': 'calendar-dot--triangle',
  'Speed & Acceleration': 'calendar-dot--diamond',
  'Easy Aerobic Run': 'calendar-dot--disc',
}
const FIELD_SHAPE_BY_NAME = {
  'Field 1': 'calendar-dot--triangle',
  'Field 2': 'calendar-dot--diamond',
}
const ULTIMATE_SHAPE_BY_NAME = {
  Solo: 'calendar-dot--ring',
  Throws: 'calendar-dot--disc',
  Pod: 'calendar-dot--disc',
  'Club Training': 'calendar-dot--triangle-hollow',
  Game: 'calendar-dot--triangle',
}

// family -> { lane dot colour class, shape map, misc shape }
const FAMILY = {
  Run: { lane: 'calendar-dot-run', shapes: RUN_SHAPE_BY_NAME, misc: 'calendar-dot--ring' },
  Field: { lane: 'calendar-dot-run', shapes: FIELD_SHAPE_BY_NAME, misc: 'calendar-dot--ring' },
  Ultimate: { lane: 'calendar-dot-ultimate', shapes: ULTIMATE_SHAPE_BY_NAME, misc: 'calendar-dot--square-hollow' },
}

// Which family an activity with no tags falls back to, from its Garmin type.
function fallbackFamily(activityType) {
  return activityType === 'Running' ? 'Run' : 'Ultimate'
}

// dayActivities: the ActivityDto[] logged on one day. Returns
// [{ key, className }] — one entry per distinct coloured shape to render,
// blue lane before teal.
export function activityDots(dayActivities) {
  const seen = new Set()
  const dots = []

  const add = (family, name) => {
    const fam = FAMILY[family] ?? FAMILY[fallbackFamily(family)]
    const shape = (name && fam.shapes[name]) || fam.misc
    const className = `calendar-dot ${fam.lane} ${shape}`
    if (seen.has(className)) return
    seen.add(className)
    dots.push({ key: className, className })
  }

  for (const a of dayActivities ?? []) {
    const tags = Array.isArray(a?.sessionTypes) ? a.sessionTypes : []
    if (tags.length === 0) {
      add(fallbackFamily(a?.type), null)
      continue
    }
    for (const t of tags) {
      add(FAMILY[t?.family] ? t.family : fallbackFamily(a?.type), t?.name)
    }
  }

  return dots.sort((x, y) => (x.className.includes('calendar-dot-run') ? 0 : 1) - (y.className.includes('calendar-dot-run') ? 0 : 1))
}

// Whether any of the day's activities is attached to a tournament - the grid
// tints the whole cell rather than adding a glyph, since a tournament is the
// day's context, not a session type.
export function isTournamentDay(dayActivities) {
  return (dayActivities ?? []).some((a) => a?.tournamentId != null)
}
