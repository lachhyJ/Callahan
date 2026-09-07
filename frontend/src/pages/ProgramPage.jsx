import { useEffect, useMemo, useState } from 'react'
import DOMPurify from 'dompurify'
import { getProgramContent } from '../api/client'

// The program doc is authored as markdown and server-rendered to HTML by
// ProgramController (raw-HTML passthrough disabled there). Rendering it inline
// rather than the old approach — fetch a PDF blob, navigate the tab to it — is
// what makes this work in the Capacitor WKWebView, which has no built-in PDF
// viewer, so that blob nav just landed on a blank page.
export default function ProgramPage() {
  const [html, setHtml] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    getProgramContent()
      .then((res) => setHtml(res.html))
      .catch((err) => setError(err.message))
  }, [])

  const clean = useMemo(() => (html === null ? null : DOMPurify.sanitize(html)), [html])

  if (error) {
    return (
      <main className="page">
        <div className="empty-state">
          <p>{error}</p>
        </div>
      </main>
    )
  }

  if (clean === null) {
    return (
      <main className="page">
        <p>Opening program…</p>
      </main>
    )
  }

  return (
    <main className="page">
      <div className="program-doc" dangerouslySetInnerHTML={{ __html: clean }} />
    </main>
  )
}
