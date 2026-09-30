export function workoutLabel(w) {
  if (w.name) return w.name
  if (w.templateName) return w.templateSubtitle ? `${w.templateName} — ${w.templateSubtitle}` : w.templateName
  return w.categorySummary ?? 'Workout'
}
