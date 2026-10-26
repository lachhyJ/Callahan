import { useEffect, useState } from 'react'
import { addQuotes, deleteQuote, getQuotes, restoreQuote } from '../api/client'
import {
  QUOTE_SOFT_LIMIT, addSummary, isLongQuote, isTooLongQuote, sortQuotes, splitQuoteLines,
} from '../utils/quotes'

// The list behind the lock-screen quotes widget: one line shown per day in the
// inline slot beside the date. Not a training feature; it lives in Callahan so
// there is one app to re-sign. Paste a whole list (one quote per line) or add them
// one at a time.
export default function QuotesPage() {
  const [quotes, setQuotes] = useState(null)
  const [draft, setDraft] = useState('')
  const [saving, setSaving] = useState(false)
  const [note, setNote] = useState('')
  const [error, setError] = useState(null)
  const [deleted, setDeleted] = useState(null)

  useEffect(() => {
    getQuotes().then(setQuotes).catch((err) => setError(err.message))
  }, [])

  const lines = splitQuoteLines(draft)
  const longLines = lines.filter(isLongQuote).length
  const tooLongLines = lines.filter(isTooLongQuote).length

  async function handleAdd(e) {
    e.preventDefault()
    if (lines.length === 0 || saving) return
    setSaving(true)
    setError(null)
    setDeleted(null)
    try {
      const result = await addQuotes(lines)
      // A quote brought back from the recovery window comes back with its old id,
      // so merge by id rather than just prepending.
      setQuotes((current) => {
        const added = new Set(result.quotes.map((q) => q.id))
        return sortQuotes([...result.quotes, ...current.filter((q) => !added.has(q.id))])
      })
      setNote(addSummary(result))
      // Keep only the lines the server refused for length, so they can be trimmed
      // and resubmitted rather than retyped. Duplicates are dropped.
      setDraft(result.skipped.filter((s) => s.reason === 'too long').map((s) => s.text).join('\n'))
    } catch (err) {
      setError(err.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(quote) {
    setError(null)
    setNote('')
    try {
      await deleteQuote(quote.id)
      setQuotes((current) => current.filter((q) => q.id !== quote.id))
      setDeleted(quote)
    } catch (err) {
      setError(err.message)
    }
  }

  async function handleUndo() {
    if (!deleted) return
    const quote = deleted
    setDeleted(null)
    try {
      const restored = await restoreQuote(quote.id)
      setQuotes((current) => sortQuotes([...current.filter((q) => q.id !== restored.id), restored]))
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <main className="page">
      <h1>Quotes</h1>
      <p className="page-subtitle">
        One of these shows beside the lock screen date each day. Only about {QUOTE_SOFT_LIMIT} characters fit.
      </p>

      {deleted && (
        <p className="delete-toast quotes-toast">Deleted. <button type="button" onClick={handleUndo}>Undo</button></p>
      )}
      {error && <p className="error">{error}</p>}

      <form className="quotes-add" onSubmit={handleAdd}>
        <label htmlFor="quotes-draft">Add quotes</label>
        <textarea
          id="quotes-draft"
          rows={3}
          value={draft}
          placeholder="One per line"
          onChange={(e) => { setDraft(e.target.value); setNote('') }}
        />
        {longLines > 0 && (
          <p className="quotes-warning">
            {longLines === 1 ? '1 is' : `${longLines} are`} over {QUOTE_SOFT_LIMIT} characters and may be cut off.
          </p>
        )}
        {tooLongLines > 0 && (
          <p className="error">{tooLongLines === 1 ? '1 is' : `${tooLongLines} are`} too long to save.</p>
        )}
        <div className="quotes-add-row">
          <button type="submit" disabled={lines.length === 0 || saving}>
            {saving ? 'Adding…' : lines.length > 1 ? `Add ${lines.length}` : 'Add'}
          </button>
          {note && <span className="quotes-note">{note}</span>}
        </div>
      </form>

      {quotes === null && !error && <p>Loading quotes…</p>}
      {quotes !== null && quotes.length === 0 && (
        <p className="page-lead">Nothing here yet. Until there is, the lock screen widget asks you to add one.</p>
      )}
      {quotes !== null && quotes.length > 0 && (
        <>
          <p className="quotes-count">{quotes.length} {quotes.length === 1 ? 'quote' : 'quotes'}</p>
          <ul className="quotes-list">
            {quotes.map((quote) => (
              <li key={quote.id} className="quotes-row">
                <span className="quotes-text">
                  {quote.text}
                  {isLongQuote(quote.text) && <span className="quotes-long">May be cut off</span>}
                </span>
                <button
                  type="button"
                  className="quotes-delete"
                  aria-label={`Delete "${quote.text}"`}
                  onClick={() => handleDelete(quote)}
                >
                  Delete
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </main>
  )
}
