import { useEffect, useMemo, useRef, useState } from 'react'
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
  const [showBackToTop, setShowBackToTop] = useState(false)
  const pageRef = useRef(null)

  useEffect(() => {
    getProgramContent()
      .then((res) => setHtml(res.html))
      .catch((err) => setError(err.message))
  }, [])

  const clean = useMemo(() => (html === null ? null : DOMPurify.sanitize(html)), [html])

  // Markdig's auto-identifiers extension already gives every heading a slug
  // id, so section links are just anchors into the sanitized markup — no
  // separate table of contents from the server needed.
  const sections = useMemo(() => {
    if (!clean) return []
    const doc = new DOMParser().parseFromString(clean, 'text/html')
    return Array.from(doc.querySelectorAll('h2'))
      .filter((h) => h.id)
      .map((h) => ({ id: h.id, text: h.textContent }))
  }, [clean])

  // .app-content (the app shell's scroll container, not window) is the only
  // scrolling element — see its comment in App.css.
  useEffect(() => {
    const scroller = pageRef.current?.closest('.app-content')
    if (!scroller) return
    const onScroll = () => setShowBackToTop(scroller.scrollTop > 400)
    scroller.addEventListener('scroll', onScroll, { passive: true })
    return () => scroller.removeEventListener('scroll', onScroll)
  }, [clean])

  const scrollToTop = () => {
    pageRef.current?.closest('.app-content')?.scrollTo({ top: 0, behavior: 'smooth' })
  }

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
    <main className="page" ref={pageRef}>
      {sections.length > 0 && (
        <nav className="program-quick-links" aria-label="Jump to section">
          {sections.map((s) => (
            <a key={s.id} href={`#${s.id}`}>{s.text}</a>
          ))}
        </nav>
      )}
      {/* clean is DOMPurify-sanitized above */}
      <div className="program-doc" dangerouslySetInnerHTML={{ __html: clean }} />
      {showBackToTop && (
        <button
          type="button"
          className="program-back-to-top"
          aria-label="Back to top"
          onClick={scrollToTop}
        >
          ↑
        </button>
      )}
    </main>
  )
}
