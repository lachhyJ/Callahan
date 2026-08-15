import { useEffect, useState } from 'react'
import { getProgramPdfBlob } from '../api/client'

// An embedded iframe only ever showed page 1 on mobile Safari — its PDF
// viewer inside an iframe doesn't get the same multi-page scroll/zoom as a
// real top-level PDF view. Replacing the tab's location with the blob URL
// hands off to that real viewer instead. It's still the same tab/session
// (no new window, no target=_blank), so the browser's own back gesture
// returns to wherever this page was opened from — nothing to confirm.
export default function ProgramPage() {
  const [error, setError] = useState(null)

  useEffect(() => {
    getProgramPdfBlob()
      .then((blob) => {
        const objectUrl = URL.createObjectURL(blob)
        window.location.replace(`${objectUrl}#view=FitH`)
      })
      .catch((err) => setError(err.message))
  }, [])

  return (
    <main className="page">
      {error ? (
        <div className="empty-state">
          <p>{error}</p>
        </div>
      ) : (
        <p>Opening program…</p>
      )}
    </main>
  )
}
