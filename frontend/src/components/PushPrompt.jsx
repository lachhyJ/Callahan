import { useEffect, useState } from 'react'
import { enablePushNotifications, hasActiveSubscription, pushSupported } from '../push'

// "Turn on notifications" for a page whose feature benefits from a push (the
// rest timer, the taper check-in reminder). Hidden until the subscription check
// comes back, so a device that's already subscribed never sees it flash up.
export default function PushPrompt({ children, buttonLabel }) {
  const [enabled, setEnabled] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    hasActiveSubscription().then(setEnabled).catch(() => {})
  }, [])

  async function enable() {
    setError(null)
    try {
      await enablePushNotifications()
      setEnabled(true)
    } catch (err) {
      setError(err.message)
    }
  }

  if (enabled || !pushSupported()) return null

  return (
    <div className="push-prompt">
      <span>{children}</span>
      <button type="button" className="secondary-btn" onClick={enable}>{buttonLabel}</button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
