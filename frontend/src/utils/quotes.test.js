import { describe, expect, it } from 'vitest'
import {
  QUOTE_MAX_LENGTH, QUOTE_SOFT_LIMIT, addSummary, isLongQuote, isTooLongQuote, sortQuotes, splitQuoteLines,
} from './quotes'

describe('splitQuoteLines', () => {
  it('makes one quote per line, trimmed, and drops blanks', () => {
    expect(splitQuoteLines('  Keep going \n\n   \r\nBe the one\r\n')).toEqual(['Keep going', 'Be the one'])
  })

  it('returns nothing for empty or whitespace-only text', () => {
    expect(splitQuoteLines('')).toEqual([])
    expect(splitQuoteLines(' \n \n')).toEqual([])
  })
})

describe('length checks', () => {
  it('warns just past the soft limit, not at it', () => {
    expect(isLongQuote('x'.repeat(QUOTE_SOFT_LIMIT))).toBe(false)
    expect(isLongQuote('x'.repeat(QUOTE_SOFT_LIMIT + 1))).toBe(true)
  })

  it('ignores surrounding whitespace', () => {
    expect(isLongQuote(`  ${'x'.repeat(QUOTE_SOFT_LIMIT)}  `)).toBe(false)
  })

  it('counts an emoji as one character', () => {
    expect(isLongQuote('🙏'.repeat(QUOTE_SOFT_LIMIT))).toBe(false)
  })

  it('flags the hard cap one past the limit', () => {
    expect(isTooLongQuote('x'.repeat(QUOTE_MAX_LENGTH))).toBe(false)
    expect(isTooLongQuote('x'.repeat(QUOTE_MAX_LENGTH + 1))).toBe(true)
  })
})

describe('sortQuotes', () => {
  it('puts the newest first and breaks a tie by id', () => {
    const quotes = [
      { id: 1, text: 'a', createdAt: '2026-10-01T00:00:00Z' },
      { id: 3, text: 'c', createdAt: '2026-10-02T00:00:00Z' },
      { id: 2, text: 'b', createdAt: '2026-10-02T00:00:00Z' },
    ]
    expect(sortQuotes(quotes).map((q) => q.text)).toEqual(['c', 'b', 'a'])
  })

  it('does not mutate its input', () => {
    const quotes = [
      { id: 1, text: 'a', createdAt: '2026-10-01T00:00:00Z' },
      { id: 2, text: 'b', createdAt: '2026-10-02T00:00:00Z' },
    ]
    sortQuotes(quotes)
    expect(quotes[0].text).toBe('a')
  })
})

describe('addSummary', () => {
  it('reports what went in and what was left out', () => {
    expect(addSummary({
      quotes: [{ id: 1 }, { id: 2 }],
      skipped: [{ text: 'x', reason: 'duplicate' }, { text: 'y', reason: 'duplicate' }, { text: 'z', reason: 'too long' }],
    })).toBe('Added 2, 2 duplicate, 1 too long.')
  })

  it('stays quiet when nothing happened', () => {
    expect(addSummary({ quotes: [], skipped: [] })).toBe('')
  })
})
