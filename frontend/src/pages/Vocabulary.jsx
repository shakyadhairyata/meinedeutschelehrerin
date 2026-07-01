import { useEffect, useMemo, useState } from 'react'
import { get, post } from '../api/client'
import { Spinner, AudioButton, Alert } from '../components/ui'
import { speak } from '../lib/speech'
import { stripArticle, checkGermanWord, fuzzyEquals, buildCloze } from '../lib/answerCheck'

const MODES = [
  { id: 'flip', label: 'Umdrehen' },
  { id: 'type', label: 'Tippen' },
  { id: 'gender', label: 'der/die/das' },
  { id: 'cloze', label: 'Lücke' },
]

// Fetch a pool of due cards and cap each mode to a session's worth. The pool is larger than the
// session so the article/cloze modes still have material even when nouns cluster later in the deck.
const POOL_SIZE = 60
const SESSION_SIZE = 20

export default function Vocabulary() {
  const [levels, setLevels] = useState([])
  const [levelId, setLevelId] = useState(null)
  const [deck, setDeck] = useState(null)
  const [mode, setMode] = useState('flip')
  const [pos, setPos] = useState(0)
  const [reloadKey, setReloadKey] = useState(0)
  const [stats, setStats] = useState({ known: 0, again: 0 })
  const [error, setError] = useState('')

  useEffect(() => {
    get('/api/levels').then((ls) => {
      setLevels(ls)
      const withVocab = ls.find((l) => l.vocabularyCount > 0) || ls[0]
      setLevelId(withVocab?.id)
    }).catch((e) => setError(e.message))
  }, [])

  useEffect(() => {
    if (!levelId) return
    setDeck(null); setPos(0); setStats({ known: 0, again: 0 })
    get(`/api/levels/${levelId}/vocabulary/due?limit=${POOL_SIZE}`).then(setDeck).catch((e) => setError(e.message))
  }, [levelId, reloadKey])

  // Some modes only apply to a subset of the due cards (gender needs a noun; cloze needs the
  // word to appear in its example). The rest feed the same spaced-repetition review.
  const activeDeck = useMemo(() => {
    if (!deck) return []
    let cards = deck
    if (mode === 'gender') cards = deck.filter((c) => c.article)
    else if (mode === 'cloze') cards = deck.filter((c) => buildCloze(c.exampleSentence, c.german, c.article))
    return cards.slice(0, SESSION_SIZE)
  }, [deck, mode])

  function switchMode(m) {
    setMode(m); setPos(0); setStats({ known: 0, again: 0 })
  }

  async function onResult(correct) {
    const card = activeDeck[pos]
    try { await post('/api/vocabulary/review', { vocabularyItemId: card.id, correct }) }
    catch (e) { setError(e.message) }
    setStats((s) => ({ known: s.known + (correct ? 1 : 0), again: s.again + (correct ? 0 : 1) }))
    setPos((p) => p + 1)
  }

  if (error) return <Alert kind="error">{error}</Alert>
  if (!deck) return <Spinner />

  const done = pos >= activeDeck.length
  const card = activeDeck[pos]

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-800">Vokabeltrainer</h1>
        <select className="input max-w-[12rem]" value={levelId || ''} onChange={(e) => setLevelId(Number(e.target.value))}>
          {levels.map((l) => <option key={l.id} value={l.id}>{l.code} ({l.vocabularyCount})</option>)}
        </select>
      </div>

      <div className="flex flex-wrap gap-2">
        {MODES.map((m) => (
          <button key={m.id} type="button" onClick={() => switchMode(m.id)}
            className={`rounded-full px-4 py-1.5 text-sm font-medium transition ${mode === m.id ? 'bg-brand-500 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
            {m.label}
          </button>
        ))}
      </div>

      {deck.length === 0 ? (
        <Alert kind="success">Keine fälligen Karten für dieses Niveau — komm später wieder!</Alert>
      ) : activeDeck.length === 0 ? (
        <Alert kind="info">Für diesen Modus gibt es bei den fälligen Karten gerade nichts zu üben. Wähle einen anderen Modus.</Alert>
      ) : done ? (
        <div className="card text-center">
          <h2 className="mt-2 text-xl font-bold">Runde geschafft!</h2>
          <p className="mt-1 text-slate-500">Gewusst: {stats.known} · Nochmal: {stats.again}</p>
          <button className="btn-primary mt-4" onClick={() => setReloadKey((k) => k + 1)}>Neue Runde</button>
        </div>
      ) : (
        <>
          <div className="text-center text-xs text-slate-400">Karte {pos + 1} / {activeDeck.length}</div>
          <ModeCard key={`${card.id}-${mode}`} mode={mode} card={card} onResult={onResult} />
        </>
      )}
    </div>
  )
}

function ModeCard({ mode, card, onResult }) {
  if (mode === 'type') return <TypeCard card={card} onResult={onResult} />
  if (mode === 'gender') return <GenderCard card={card} onResult={onResult} />
  if (mode === 'cloze') return <ClozeCard card={card} onResult={onResult} />
  return <FlipCard card={card} onResult={onResult} />
}

function FlipCard({ card, onResult }) {
  const [flipped, setFlipped] = useState(false)
  return (
    <>
      <button type="button"
        onClick={() => { setFlipped((f) => !f); if (!flipped) speak(card.german) }}
        className="card flex min-h-[220px] w-full flex-col items-center justify-center gap-3 text-center transition hover:shadow-md">
        {!flipped ? (
          <>
            <div className="text-3xl font-bold text-slate-800">
              {card.article && <span className="text-brand-600">{card.article} </span>}{stripArticle(card.german, card.article)}
            </div>
            <span className="text-xs text-slate-400">Tippen zum Umdrehen · Box {card.box}</span>
          </>
        ) : (
          <>
            <div className="text-2xl font-semibold text-slate-800">{card.english}</div>
            <div className="text-sm italic text-slate-500">„{card.exampleSentence}“</div>
            {card.plural && <div className="text-xs text-slate-400">Plural: {card.plural}</div>}
            {card.note && <div className="text-xs text-slate-400">{card.note}</div>}
          </>
        )}
      </button>
      <div className="flex items-center justify-center gap-2"><AudioButton text={card.german} label="Aussprache" /></div>
      {flipped && (
        <div className="grid grid-cols-2 gap-3">
          <button className="btn bg-rose-100 text-rose-700 hover:bg-rose-200" onClick={() => onResult(false)}>Nochmal</button>
          <button className="btn-success" onClick={() => onResult(true)}>Gewusst ✓</button>
        </div>
      )}
    </>
  )
}

function TypeCard({ card, onResult }) {
  const [value, setValue] = useState('')
  const [checked, setChecked] = useState(null)
  function submit(e) {
    e.preventDefault()
    if (checked) return
    setChecked({ correct: checkGermanWord(value, card.german, card.article) })
  }
  return (
    <form onSubmit={submit} className="card space-y-4 text-center">
      <div className="text-xs uppercase tracking-wide text-slate-400">
        {card.article ? 'Substantiv – tippe mit Artikel' : 'Wie heißt das auf Deutsch?'}
      </div>
      <div className="text-2xl font-bold text-slate-800">{card.english}</div>
      {!checked ? (
        <>
          <input autoFocus className="input text-center text-lg" value={value}
            onChange={(e) => setValue(e.target.value)} placeholder="Deutsches Wort…" />
          <button type="submit" className="btn-primary w-full" disabled={!value.trim()}>Prüfen</button>
        </>
      ) : (
        <>
          <Alert kind={checked.correct ? 'success' : 'error'}>
            {checked.correct ? '✓ Richtig!' : '✗ Nicht ganz.'} Lösung: <strong>{card.german}</strong>
          </Alert>
          <div className="text-sm italic text-slate-500">„{card.exampleSentence}“</div>
          <div className="flex justify-center"><AudioButton text={card.german} label="Aussprache" /></div>
          <button type="button" className="btn-primary w-full" onClick={() => onResult(checked.correct)}>Weiter →</button>
        </>
      )}
    </form>
  )
}

function GenderCard({ card, onResult }) {
  const [picked, setPicked] = useState(null)
  const noun = stripArticle(card.german, card.article)
  return (
    <div className="card space-y-4 text-center">
      <div className="text-xs uppercase tracking-wide text-slate-400">Welcher Artikel?</div>
      <div className="text-3xl font-bold text-slate-800">{noun}</div>
      <div className="grid grid-cols-3 gap-3">
        {['der', 'die', 'das'].map((a) => {
          let cls = 'btn bg-slate-100 text-slate-700 hover:bg-slate-200'
          if (picked) {
            if (a === card.article) cls = 'btn bg-emerald-500 text-white'
            else if (a === picked) cls = 'btn bg-rose-500 text-white'
            else cls = 'btn bg-slate-100 text-slate-400'
          }
          return (
            <button key={a} type="button" className={cls} disabled={!!picked} onClick={() => setPicked(a)}>{a}</button>
          )
        })}
      </div>
      {picked && (
        <>
          <div className="text-sm text-slate-500">
            <strong>{card.german}</strong> — {card.english}
          </div>
          <div className="text-sm italic text-slate-500">„{card.exampleSentence}“</div>
          <div className="flex justify-center"><AudioButton text={card.german} label="Aussprache" /></div>
          <button type="button" className="btn-primary w-full" onClick={() => onResult(picked === card.article)}>Weiter →</button>
        </>
      )}
    </div>
  )
}

function ClozeCard({ card, onResult }) {
  const cloze = useMemo(() => buildCloze(card.exampleSentence, card.german, card.article), [card])
  const [value, setValue] = useState('')
  const [checked, setChecked] = useState(null)
  function submit(e) {
    e.preventDefault()
    if (checked) return
    setChecked({ correct: fuzzyEquals(value, cloze.answer) })
  }
  return (
    <form onSubmit={submit} className="card space-y-4 text-center">
      <div className="text-xs uppercase tracking-wide text-slate-400">Ergänze das Wort · {card.english}</div>
      <div className="text-lg text-slate-800">
        {cloze.before}
        <span className="font-bold text-brand-600">{checked ? cloze.answer : '＿＿＿'}</span>
        {cloze.after}
      </div>
      {!checked ? (
        <>
          <input autoFocus className="input text-center text-lg" value={value}
            onChange={(e) => setValue(e.target.value)} placeholder="Fehlendes Wort…" />
          <button type="submit" className="btn-primary w-full" disabled={!value.trim()}>Prüfen</button>
        </>
      ) : (
        <>
          <Alert kind={checked.correct ? 'success' : 'error'}>
            {checked.correct ? '✓ Richtig!' : '✗ Nicht ganz.'} Lösung: <strong>{cloze.answer}</strong>
          </Alert>
          <div className="flex justify-center"><AudioButton text={card.german} label="Aussprache" /></div>
          <button type="button" className="btn-primary w-full" onClick={() => onResult(checked.correct)}>Weiter →</button>
        </>
      )}
    </form>
  )
}
