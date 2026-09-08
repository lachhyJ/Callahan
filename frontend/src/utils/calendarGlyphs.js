// Calendar day-cell glyphs. Colour = family lane (blue for Run/Field, teal for
// Ultimate); shape = the session subtype within that lane. A shared 4-shape
// vocabulary (disc / triangle / diamond / ring) is reused across lanes since
// colour already separates them. Ultimate's types are grouped down a bit
// (Throws+Pod → ring, Club Training+Game → triangle) to keep the read easy;
// unclassified activities fall back to their lane's misc shape.
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
  'Field 1 - Acceleration & Jump Quality': 'calendar-dot--triangle',
  'Field 2 - Repeat Effort & COD': 'calendar-dot--diamond',
}
const ULTIMATE_SHAPE_BY_NAME = {
  Solo: 'calendar-dot--disc',
  Throws: 'calendar-dot--ring',
  Pod: 'calendar-dot--ring',
  'Club Training': 'calendar-dot--triangle',
  Game: 'calendar-dot--triangle',
}

// family -> { lane dot colour class, shape map, misc shape }
const FAMILY = {
  Run: { lane: 'calendar-dot-run', shapes: RUN_SHAPE_BY_NAME, misc: 'calendar-dot--ring' },
  Field: { lane: 'calendar-dot-run', shapes: FIELD_SHAPE_BY_NAME, misc: 'calendar-dot--ring' },
  Ultimate: { lane: 'calendar-dot-ultimate', shapes: ULTIMATE_SHAPE_BY_NAME, misc: 'calendar-dot--disc' },
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
