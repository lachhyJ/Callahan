import { useCallback, useState } from 'react'
import { getActivitySessionTypes, updateActivitySessionTags } from '../api/client'

// Tags an Ultimate activity as "Game" (primary), keeping any labels it already
// had as extras, and offers an undo that restores the exact previous tag set.
// `onChanged` reloads the caller's lists after either direction.
export default function useMarkAsGame(onChanged) {
  const [toast, setToast] = useState(null) // { message, undo } | null
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)

  const markAsGame = useCallback(async (activity) => {
    setBusy(true)
    setError(null)
    try {
      const types = await getActivitySessionTypes()
      const game = types.find((t) => t.name === 'Game')
      if (!game) throw new Error('No "Game" session type found.')
      const prevPrimary = activity.activitySessionTypeId ?? null
      const prevIds = (activity.sessionTypes ?? []).map((t) => t.id ?? t.activitySessionTypeId)
      const typeIds = [...new Set([...prevIds, game.id ?? game.activitySessionTypeId])]
      await updateActivitySessionTags(activity.id, {
        primaryId: game.id ?? game.activitySessionTypeId,
        typeIds,
      })
      setToast({
        message: 'Marked as Game',
        undo: async () => {
          setToast(null)
          try {
            await updateActivitySessionTags(activity.id, { primaryId: prevPrimary, typeIds: prevIds })
            onChanged()
          } catch (err) {
            setError(err.message)
          }
        },
      })
      onChanged()
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }, [onChanged])

  const dismiss = useCallback(() => setToast(null), [])
  return { markAsGame, busy, error, toast, dismiss }
}
