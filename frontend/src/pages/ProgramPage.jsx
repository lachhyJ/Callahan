import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getProgramPdfBlob } from '../api/client'
import { BackIcon } from '../icons'

export default function ProgramPage() {
  const navigate = useNavigate()
  const [pdfUrl, setPdfUrl] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    let objectUrl = null
    getProgramPdfBlob()
      .then((blob) => {
        objectUrl = URL.createObjectURL(blob)
        // #view=FitH asks the PDF viewer to open fit-to-width instead of its
        // 100%-zoom default, which otherwise crops the page on mobile.
        setPdfUrl(`${objectUrl}#view=FitH`)
      })
      .catch((err) => setError(err.message))
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [])

  return (
    <main className="page program-page">
      <div className="program-page-header">
        <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      </div>

      {error && (
        <div className="empty-state">
          <p>{error}</p>
        </div>
      )}
      {!error && !pdfUrl && <p>Loading…</p>}

      {pdfUrl && <iframe src={pdfUrl} title="Training program" className="program-pdf-frame" />}
    </main>
  )
}
