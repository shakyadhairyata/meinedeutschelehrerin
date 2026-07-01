import { describe, it, expect } from 'vitest'
import { normalize, levenshtein, fuzzyEquals, stripArticle, checkGermanWord, buildCloze } from './answerCheck'

describe('normalize', () => {
  it('lowercases, trims and strips punctuation', () => {
    expect(normalize('  „Die Prämisse!" ')).toBe('die prämisse')
  })
})

describe('levenshtein', () => {
  it('counts single edits', () => {
    expect(levenshtein('katze', 'katzen')).toBe(1)
    expect(levenshtein('haus', 'haus')).toBe(0)
  })
})

describe('fuzzyEquals', () => {
  it('matches exactly (case-insensitive)', () => {
    expect(fuzzyEquals('Haus', 'haus')).toBe(true)
  })
  it('tolerates one typo in a longer word', () => {
    expect(fuzzyEquals('Prämise', 'Prämisse')).toBe(true)
  })
  it('rejects a clearly different word', () => {
    expect(fuzzyEquals('Auto', 'Haus')).toBe(false)
  })
  it('is strict on short words', () => {
    expect(fuzzyEquals('Tee', 'See')).toBe(false)
  })
})

describe('stripArticle', () => {
  it('removes a leading article', () => {
    expect(stripArticle('die Prämisse', 'die')).toBe('Prämisse')
  })
  it('leaves verbs and adjectives untouched', () => {
    expect(stripArticle('legitimieren', null)).toBe('legitimieren')
  })
})

describe('checkGermanWord', () => {
  it('accepts the full form with article', () => {
    expect(checkGermanWord('die Prämisse', 'die Prämisse', 'die')).toBe(true)
  })
  it('accepts the noun without its article', () => {
    expect(checkGermanWord('Prämisse', 'die Prämisse', 'die')).toBe(true)
  })
  it('rejects a wrong word', () => {
    expect(checkGermanWord('Hypothese', 'die Prämisse', 'die')).toBe(false)
  })
})

describe('buildCloze', () => {
  it('blanks the noun where it appears in the example', () => {
    const c = buildCloze('Mein Kopf tut weh.', 'der Kopf', 'der')
    expect(c).toEqual({ before: 'Mein ', answer: 'Kopf', after: ' tut weh.' })
  })
  it('returns null when the word is not in the example verbatim', () => {
    // conjugated verb: headword "geben" does not appear in "Gib mir das Buch."
    expect(buildCloze('Gib mir das Buch.', 'geben', null)).toBeNull()
  })
})
