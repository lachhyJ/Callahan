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
        setPdfUrl(objectUrl)
      })
      .catch((err) => setError(err.message))
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [])

  return (
    <main className="page program-page">
      <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
      <h1>Program</h1>

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
