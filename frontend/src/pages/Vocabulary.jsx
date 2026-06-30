import { useEffect, useState } from 'react'
import { get, post } from '../api/client'
import { Spinner, AudioButton, Alert } from '../components/ui'
import { speak } from '../lib/speech'

export default function Vocabulary() {
  const [levels, setLevels] = useState([])
  const [levelId, setLevelId] = useState(null)
  const [deck, setDeck] = useState(null)
  const [pos, setPos] = useState(0)
  const [flipped, setFlipped] = useState(false)
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
    setDeck(null); setPos(0); setFlipped(false); setStats({ known: 0, again: 0 })
    get(`/api/levels/${levelId}/vocabulary/due?limit=20`).then(setDeck).catch((e) => setError(e.message))
  }, [levelId])

  async function review(correct) {
    const card = deck[pos]
    try { await post('/api/vocabulary/review', { vocabularyItemId: card.id, correct }) }
    catch (e) { setError(e.message) }
    setStats((s) => ({ known: s.known + (correct ? 1 : 0), again: s.again + (correct ? 0 : 1) }))
    setFlipped(false)
    setPos((p) => p + 1)
  }

  if (error) return <Alert kind="error">{error}</Alert>
  if (!deck) return <Spinner />

  const done = pos >= deck.length
  const card = deck[pos]

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-800">Vokabeltrainer</h1>
        <select className="input max-w-[12rem]" value={levelId || ''} onChange={(e) => setLevelId(Number(e.target.value))}>
          {levels.map((l) => <option key={l.id} value={l.id}>{l.code} ({l.vocabularyCount})</option>)}
        </select>
      </div>

      {deck.length === 0 ? (
        <Alert kind="success">Keine fälligen Karten für dieses Niveau — komm später wieder! 🎉</Alert>
      ) : done ? (
        <div className="card text-center">
          <div className="text-4xl">🎉</div>
          <h2 className="mt-2 text-xl font-bold">Runde geschafft!</h2>
          <p className="mt-1 text-slate-500">Gewusst: {stats.known} · Nochmal: {stats.again}</p>
          <button className="btn-primary mt-4" onClick={() => setLevelId(levelId)}>Neue Runde</button>
        </div>
      ) : (
        <>
          <div className="text-center text-xs text-slate-400">Karte {pos + 1} / {deck.length}</div>
          <button
            type="button"
            onClick={() => { setFlipped((f) => !f); if (!flipped) speak(card.article ? `${card.article} ${card.german}` : card.german) }}
            className="card flex min-h-[220px] w-full flex-col items-center justify-center gap-3 text-center transition hover:shadow-md">
            {!flipped ? (
              <>
                <div className="text-3xl font-bold text-slate-800">
                  {card.article && <span className="text-brand-600">{card.article} </span>}{card.german}
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

          <div className="flex items-center justify-center gap-2">
            <AudioButton text={card.article ? `${card.article} ${card.german}` : card.german} label="Aussprache" />
          </div>

          {flipped && (
            <div className="grid grid-cols-2 gap-3">
              <button className="btn bg-rose-100 text-rose-700 hover:bg-rose-200" onClick={() => review(false)}>Nochmal</button>
              <button className="btn-success" onClick={() => review(true)}>Gewusst ✓</button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
