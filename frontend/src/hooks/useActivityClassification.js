import { useEffect, useState } from 'react'
import { getActivitySessionTypes, updateActivitySessionTags, updateConeDistance } from '../api/client'

// The session-type list is a small, seed-managed catalog, fetched once per app
// load and shared. Each hook instance fetching its own made the streak detail
// page (one SessionList per week) send the same request once per week row.
let sessionTypesPromise = null

function loadSessionTypes() {
  sessionTypesPromise ??= getActivitySessionTypes().catch((err) => {
    sessionTypesPromise = null // let the next mount retry
    throw err
  })
  return sessionTypesPromise
}

// Shared by every place an activity's session type gets classified
// (SessionList's compact preview, HistoryPage's full log) — same picker-open
// state and type list either way, just a different place to apply the result
// once the API call resolves.
export function useActivityClassification(onUpdate) {
  const [sessionTypes, setSessionTypes] = useState([])
  const [openPickerId, setOpenPickerId] = useState(null)

  useEffect(() => {
    let cancelled = false
    loadSessionTypes().then((types) => { if (!cancelled) setSessionTypes(types) }).catch(() => {})
    return () => { cancelled = true }
  }, [])

  function togglePicker(activityId) {
    setOpenPickerId((current) => (current === activityId ? null : activityId))
  }

  // tags: { primaryId, typeIds }. Replaces the activity's whole tag set.
  async function saveSessionTags(activityId, tags) {
    setOpenPickerId(null)
    const updated = await updateActivitySessionTags(activityId, tags)
    onUpdate(updated)
  }

  async function setConeDistance(activityId, coneDistanceM) {
    const updated = await updateConeDistance(activityId, coneDistanceM)
    onUpdate(updated)
  }

  return { sessionTypes, openPickerId, togglePicker, saveSessionTags, setConeDistance }
}
