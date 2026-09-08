import { useEffect, useState } from 'react'
import { getActivitySessionTypes, updateActivitySessionTags, updateConeDistance } from '../api/client'

// Shared by every place an activity's session type gets classified
// (SessionList's compact preview, HistoryPage's full log) — same picker-open
// state and fetch-once type list either way, just a different place to apply
// the result once the API call resolves.
export function useActivityClassification(onUpdate) {
  const [sessionTypes, setSessionTypes] = useState([])
  const [openPickerId, setOpenPickerId] = useState(null)

  useEffect(() => {
    getActivitySessionTypes().then(setSessionTypes).catch(() => {})
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
