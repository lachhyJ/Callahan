import { isoDate, startOfWeek } from '../dateUtils'

// The ankle circuit's id: the planner pins it the same way (PlanWeekLoader).
export const ANKLE_ROUTINE_ID = 1

// The doc asks for the ankle circuit at the start of every session, at least this many
// days a week. Shown on the Dashboard tile so the weekly count is visible at a glance.
export const ANKLE_WEEKLY_TARGET = 3

// How many distinct days this (Monday-first) week the routine was ticked. Completions are
// one row per day, so this is a count of rows whose date falls in the week.
export function routineDaysThisWeek(routine, todayIso) {
  if (!routine?.recent) return 0
  const start = isoDate(startOfWeek(new Date(`${todayIso}T00:00:00`)))
  return routine.recent.filter((c) => c.date >= start && c.date <= todayIso).length
}
