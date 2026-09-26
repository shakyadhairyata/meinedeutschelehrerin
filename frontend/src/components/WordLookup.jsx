import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { renderMarkdownLookup } from '../lib/markdown'
import { lookupWord } from '../api/client'

// Session cache: normalized word -> lookup result. Shared across lessons for the whole visit, so
// re-hovering a word is instant and never re-fetches (and never re-spends an AI credit).
const cache = new Map()
const normalize = (w) => (w || '').toLowerCase().replace(/[^\p{L}-]/gu, '')

/**
 * Renders trusted lesson Markdown with every word made hoverable/tappable. Desktop: hover a word
 * (short delay) to see its meaning; the popover stays until you look at another word, click away,
 * scroll, or press Escape. Mobile/touch: tap a word (hover doesn't exist there).
 */
export default function LessonText({ content, level }) {
  const html = useMemo(() => renderMarkdownLookup(content || ''), [content])
  const containerRef = useRef(null)
  const hoverTimer = useRef(null)
  const [pop, setPop] = useState(null) // { x, y, word, loading, data, error }

  const runLookup = useCallback(async (word, el) => {
    const key = normalize(word)
    if (!key) return
    const rect = el.getBoundingClientRect()
    const anchor = { x: rect.left + rect.width / 2, y: rect.bottom }
    if (cache.has(key)) {
      setPop({ ...anchor, word, loading: false, data: cache.get(key) })
      return
    }
    setPop({ ...anchor, word, loading: true, data: null })
    try {
      const res = await lookupWord(word, level)
      cache.set(key, res)
      setPop((p) => (p && p.word === word ? { ...p, loading: false, data: res } : p))
    } catch (e) {
      setPop((p) => (p && p.word === word ? { ...p, loading: false, error: e.message } : p))
    }
  }, [level])

  const wordAt = (e) => {
    const el = e.target.closest?.('.lw')
    return el && containerRef.current?.contains(el) ? el : null
  }

  function onMouseOver(e) {
    const el = wordAt(e)
    if (!el) return
    clearTimeout(hoverTimer.current)
    const word = el.textContent
    hoverTimer.current = setTimeout(() => runLookup(word, el), 250)
  }
  function onMouseOut() {
    clearTimeout(hoverTimer.current) // cancel a pending open; leave an open popover in place
  }
  function onClick(e) {
    const el = wordAt(e)
    if (!el) return
    clearTimeout(hoverTimer.current)
    runLookup(el.textContent, el)
  }

  useEffect(() => {
    if (!pop) return
    const onDocDown = (e) => { if (!e.target.closest?.('.wordpop') && !e.target.closest?.('.lw')) setPop(null) }
    const onKey = (e) => { if (e.key === 'Escape') setPop(null) }
    const onScroll = () => setPop(null)
    document.addEventListener('mousedown', onDocDown)
    document.addEventListener('keydown', onKey)
    window.addEventListener('scroll', onScroll, true)
    return () => {
      document.removeEventListener('mousedown', onDocDown)
      document.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', onScroll, true)
    }
  }, [pop])

  useEffect(() => () => clearTimeout(hoverTimer.current), [])

  return (
    <>
      <div
        ref={containerRef}
        className="prose-lesson"
        onMouseOver={onMouseOver}
        onMouseOut={onMouseOut}
        onClick={onClick}
        dangerouslySetInnerHTML={{ __html: html }}
      />
      {pop && <WordPopover pop={pop} />}
    </>
  )
}

function WordPopover({ pop }) {
  const style = {
    position: 'fixed',
    left: Math.min(Math.max(pop.x, 96), (typeof window !== 'undefined' ? window.innerWidth : 400) - 96),
    top: pop.y + 8,
    transform: 'translateX(-50%)',
    zIndex: 60,
  }
  const d = pop.data
  return (
    <div className="wordpop" style={style} role="tooltip">
      {pop.loading && <div className="wordpop-muted">Nachschlagen…</div>}
      {!pop.loading && pop.error && <div className="wordpop-muted">Nachschlagen momentan nicht möglich.</div>}
      {!pop.loading && d && !d.found && (
        <div className="wordpop-muted">„{pop.word}“ – kein Eintrag gefunden.</div>
      )}
      {!pop.loading && d && d.found && (
        <>
          <div className="wordpop-head">{d.article ? `${d.article} ` : ''}{d.german}</div>
          <div className="wordpop-en">
            {d.english}
            {d.partOfSpeech ? ` · ${d.partOfSpeech}` : ''}
            {d.plural ? ` · Pl. ${d.plural}` : ''}
          </div>
          {d.example && <div className="wordpop-ex">„{d.example}“</div>}
          {d.note && <div className="wordpop-note">{d.note}</div>}
          {d.source === 'ai' && <div className="wordpop-tag">✨ Neu – zur Vokabelliste hinzugefügt</div>}
        </>
      )}
    </div>
  )
}
