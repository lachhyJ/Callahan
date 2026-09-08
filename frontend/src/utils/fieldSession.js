// A "field session" is the program's Field 1 / Field 2 work. Its session type
// has family 'Field' regardless of whether Garmin recorded the activity as a
// Running or an Ultimate event — so it's detected off the tag family, not the
// activity's type. The name-prefix check is a fallback for an activity DTO
// that predates the multi-tag `sessionTypes` array.
export function isFieldActivity(activity) {
  const tags = activity?.sessionTypes
  if (Array.isArray(tags) && tags.length > 0) {
    return tags.some((t) => t.family === 'Field')
  }
  return typeof activity?.activitySessionTypeName === 'string'
    && activity.activitySessionTypeName.startsWith('Field ')
}
