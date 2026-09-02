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
        // Handing the URL to location.replace navigates this document away, so
        // there's no unmount to revoke on — but the blob stays alive for the
        // lifetime of the tab otherwise. pagehide fires on the way out (and,
        // unlike unload, is reliable on iOS Safari), which is late enough for
        // the viewer to have taken the data and early enough to not leak a
        // PDF-sized buffer per visit.
        window.addEventListener('pagehide', () => URL.revokeObjectURL(objectUrl), { once: true })
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
