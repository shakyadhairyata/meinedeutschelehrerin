import { useMemo, useRef, useState } from 'react'
import { post } from '../api/client'
import { AudioButton, SkillBadge, Alert, RichText } from './ui'
import { getRecognition, recognitionSupported } from '../lib/speech'

const CHOICE_TYPES = ['MultipleChoice', 'ReadingComprehension', 'ListeningComprehension']

export default function ExercisePlayer({ exercise, index, total, onResult, onNext }) {
  const { type, content } = exercise
  const startRef = useRef(Date.now())
  const [response, setResponse] = useState(initialResponse(type, content))
  const [result, setResult] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function submit() {
    setSubmitting(true)
    setError('')
    try {
      const timeSpentSeconds = Math.round((Date.now() - startRef.current) / 1000)
      const payload = toPayload(type, response, content)
      const res = await post('/api/exercises/submit', { exerciseId: exercise.id, response: payload, timeSpentSeconds })
      setResult(res)
      onResult?.(res)
    } catch (e) {
      setError(e.message)
    } finally {
      setSubmitting(false)
    }
  }

  const graded = result !== null
  const canSubmit = isAnswered(type, response)

  return (
    <div className="card">
      <div className="mb-3 flex items-center justify-between">
        <SkillBadge skill={exercise.skill} />
        <span className="text-xs text-slate-400">
          {typeof index === 'number' ? `Übung ${index + 1} / ${total}` : ''} · {exercise.points} Pkt
        </span>
      </div>

      <p className="mb-4 font-medium text-slate-800">{exercise.prompt}</p>

      <Widget type={type} content={content} response={response} setResponse={setResponse} disabled={graded} result={result} />

      {error && <div className="mt-3"><Alert kind="error">{error}</Alert></div>}

      {graded && <Feedback type={type} result={result} />}

      <div className="mt-4 flex justify-end gap-2">
        {!graded && (
          <button className="btn-primary" disabled={!canSubmit || submitting} onClick={submit}>
            {submitting ? 'Prüfe…' : 'Antwort prüfen'}
          </button>
        )}
        {graded && onNext && (
          <button className="btn-success" onClick={onNext}>Weiter →</button>
        )}
      </div>
    </div>
  )
}

// ---------------- per-type widgets ----------------

function Widget({ type, content, response, setResponse, disabled, result }) {
  if (CHOICE_TYPES.includes(type)) {
    return <ChoiceWidget type={type} content={content} response={response} setResponse={setResponse} disabled={disabled} result={result} />
  }
  switch (type) {
    case 'FillInBlank':
    case 'Cloze':
      return <BlanksWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Reorder':
      return <ReorderWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Matching':
      return <MatchingWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Dictation':
      return <DictationWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Conjugation':
      return <ConjugationWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Translation':
      return <TranslationWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Writing':
      return <WritingWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    case 'Speaking':
      return <SpeakingWidget content={content} response={response} setResponse={setResponse} disabled={disabled} />
    default:
      return <Alert kind="error">Unbekannter Übungstyp: {type}</Alert>
  }
}

function ChoiceWidget({ type, content, response, setResponse, disabled, result }) {
  const correct = result?.correctAnswer
  return (
    <div className="space-y-3">
      {type === 'ListeningComprehension' && content?.audioText && (
        <div className="flex items-center gap-3 rounded-lg bg-emerald-50 p-3">
          <AudioButton text={content.audioText} label="Hörtext abspielen" />
          <span className="text-xs text-emerald-700">Hör genau zu und wähle die richtige Antwort.</span>
        </div>
      )}
      {content?.question && <p className="font-medium">{content.question}</p>}
      <div className="grid gap-2">
        {(content?.options || []).map((opt, i) => {
          const selected = response.selectedIndex === i
          let cls = 'btn-ghost justify-start text-left'
          if (disabled) {
            if (i === correct) cls = 'btn justify-start bg-emerald-100 text-emerald-800 ring-1 ring-emerald-300'
            else if (selected) cls = 'btn justify-start bg-rose-100 text-rose-800 ring-1 ring-rose-300'
            else cls = 'btn-ghost justify-start text-left opacity-60'
          } else if (selected) {
            cls = 'btn justify-start bg-brand-600 text-white'
          }
          return (
            <button key={i} type="button" className={cls} disabled={disabled}
              onClick={() => setResponse({ selectedIndex: i })}>
              <span className="mr-2 font-mono text-xs opacity-70">{String.fromCharCode(65 + i)}</span>{opt}
            </button>
          )
        })}
      </div>
    </div>
  )
}

function BlanksWidget({ content, response, setResponse, disabled }) {
  const segments = useMemo(() => (content?.text || '').split('___'), [content])
  const answers = response.answers
  function set(i, val) {
    const next = [...answers]; next[i] = val; setResponse({ answers: next })
  }
  return (
    <p className="flex flex-wrap items-center gap-1 leading-9">
      {segments.map((seg, i) => (
        <span key={i} className="flex items-center gap-1">
          <span>{seg}</span>
          {i < segments.length - 1 && (
            <input className="input inline-block w-28 px-2 py-1" value={answers[i] || ''} disabled={disabled}
              onChange={(e) => set(i, e.target.value)} placeholder={content?.blanks?.[i]?.hint || '…'} />
          )}
        </span>
      ))}
    </p>
  )
}

function ReorderWidget({ content, response, setResponse, disabled }) {
  const tokens = content?.tokens || []
  const chosen = response.indexes // array of token indexes in chosen order
  const used = new Set(chosen)
  function pick(i) { if (!used.has(i)) setResponse({ indexes: [...chosen, i] }) }
  function removeAt(pos) { setResponse({ indexes: chosen.filter((_, p) => p !== pos) }) }
  return (
    <div className="space-y-3">
      <div className="flex min-h-[2.5rem] flex-wrap gap-2 rounded-lg border-2 border-dashed border-slate-200 p-2">
        {chosen.length === 0 && <span className="text-sm text-slate-400">Tippe die Wörter in der richtigen Reihenfolge an…</span>}
        {chosen.map((tIdx, pos) => (
          <button key={pos} type="button" disabled={disabled} className="chip bg-brand-600 px-3 py-1 text-white"
            onClick={() => removeAt(pos)}>{tokens[tIdx]}</button>
        ))}
      </div>
      <div className="flex flex-wrap gap-2">
        {tokens.map((t, i) => (
          <button key={i} type="button" disabled={disabled || used.has(i)}
            className={`chip px-3 py-1 ${used.has(i) ? 'bg-slate-100 text-slate-300' : 'bg-slate-200 text-slate-700 hover:bg-slate-300'}`}
            onClick={() => pick(i)}>{t}</button>
        ))}
      </div>
    </div>
  )
}

function MatchingWidget({ content, response, setResponse, disabled }) {
  const left = content?.left || []
  const right = content?.right || []
  const pairs = response.pairs // [[leftIdx, rightIdx]]
  function setPair(li, ri) {
    const next = pairs.filter((p) => p[0] !== li)
    if (ri !== '') next.push([li, Number(ri)])
    setResponse({ pairs: next })
  }
  const valueFor = (li) => { const p = pairs.find((x) => x[0] === li); return p ? p[1] : '' }
  return (
    <div className="space-y-2">
      {left.map((l, li) => (
        <div key={li} className="flex items-center gap-3">
          <span className="w-40 font-medium">{l}</span>
          <span className="text-slate-400">→</span>
          <select className="input max-w-xs" value={valueFor(li)} disabled={disabled}
            onChange={(e) => setPair(li, e.target.value)}>
            <option value="">— wählen —</option>
            {right.map((r, ri) => <option key={ri} value={ri}>{r}</option>)}
          </select>
        </div>
      ))}
    </div>
  )
}

function DictationWidget({ content, response, setResponse, disabled }) {
  return (
    <div className="space-y-3">
      <AudioButton text={content?.audioText || ''} label="Abspielen" />
      <textarea className="input min-h-[80px]" value={response.text} disabled={disabled}
        onChange={(e) => setResponse({ text: e.target.value })} placeholder="Schreib, was du hörst…" />
    </div>
  )
}

function ConjugationWidget({ content, response, setResponse, disabled }) {
  return (
    <div className="flex items-center gap-3">
      <span className="rounded-lg bg-slate-100 px-3 py-2 text-sm">
        <strong>{content?.verb}</strong> · {content?.person} · {content?.tense}
      </span>
      <input className="input max-w-xs" value={response.answer} disabled={disabled}
        onChange={(e) => setResponse({ answer: e.target.value })} placeholder="Form eingeben…" />
    </div>
  )
}

function TranslationWidget({ content, response, setResponse, disabled }) {
  return (
    <div className="space-y-2">
      <p className="rounded-lg bg-slate-100 px-3 py-2 text-sm italic">{content?.source}</p>
      <textarea className="input min-h-[70px]" value={response.text} disabled={disabled}
        onChange={(e) => setResponse({ text: e.target.value })} placeholder="Übersetzung…" />
    </div>
  )
}

function WritingWidget({ content, response, setResponse, disabled }) {
  const words = response.text.trim() ? response.text.trim().split(/\s+/).length : 0
  const min = content?.minWords || 0
  return (
    <div className="space-y-2">
      <textarea className="input min-h-[160px]" value={response.text} disabled={disabled}
        onChange={(e) => setResponse({ text: e.target.value })} placeholder="Schreib deinen Text auf Deutsch…" />
      <div className={`text-xs ${words >= min ? 'text-emerald-600' : 'text-slate-400'}`}>
        {words} Wörter{min ? ` (mind. ${min})` : ''}
      </div>
    </div>
  )
}

function SpeakingWidget({ content, response, setResponse, disabled }) {
  const [listening, setListening] = useState(false)
  const supported = recognitionSupported()

  function record() {
    const r = getRecognition()
    if (!r) return
    setListening(true)
    r.onresult = (e) => setResponse({ transcript: e.results[0][0].transcript })
    r.onerror = () => setListening(false)
    r.onend = () => setListening(false)
    r.start()
  }

  return (
    <div className="space-y-3">
      {content?.targetText && (
        <div className="flex items-center gap-3 rounded-lg bg-rose-50 p-3">
          <AudioButton text={content.targetText} label="Vorbild hören" />
          <span className="text-sm text-rose-700">„{content.targetText}“</span>
        </div>
      )}
      {supported ? (
        <button type="button" className={listening ? 'btn bg-rose-600 text-white' : 'btn-ghost'} disabled={disabled} onClick={record}>
          {listening ? '● Aufnahme läuft…' : '🎙️ Sprechen & aufnehmen'}
        </button>
      ) : (
        <p className="text-xs text-slate-400">Spracherkennung nicht verfügbar — tippe deine Antwort unten.</p>
      )}
      <textarea className="input min-h-[70px]" value={response.transcript} disabled={disabled}
        onChange={(e) => setResponse({ transcript: e.target.value })} placeholder="Transkription / Antwort…" />
    </div>
  )
}

// ---------------- feedback ----------------

function Feedback({ type, result }) {
  const isAi = type === 'Writing' || type === 'Speaking'
  const fb = result.feedback
  return (
    <div className={`mt-4 rounded-lg p-4 ring-1 ${result.isCorrect ? 'bg-emerald-50 ring-emerald-200' : 'bg-amber-50 ring-amber-200'}`}>
      <div className="flex items-center justify-between">
        <span className={`font-semibold ${result.isCorrect ? 'text-emerald-700' : 'text-amber-700'}`}>
          {result.isCorrect ? '✓ Richtig!' : '✗ Noch nicht ganz'}
        </span>
        <span className="text-sm font-medium text-slate-600">{result.scorePercent}%</span>
      </div>
      {result.explanation && <p className="mt-2 text-sm text-slate-700"><RichText>{result.explanation}</RichText></p>}
      {!isAi && result.correctAnswer != null && !result.isCorrect && (
        <p className="mt-1 text-sm text-slate-600">Lösung: <strong>{formatAnswer(result.correctAnswer)}</strong></p>
      )}
      {isAi && fb && (
        <div className="mt-3 space-y-2 text-sm">
          {fb.summary && <p className="text-slate-700">{fb.summary}</p>}
          {fb.cefrEstimate && <p className="text-xs text-slate-500">Geschätztes Niveau: <strong>{fb.cefrEstimate}</strong></p>}
          {fb.strengths?.length > 0 && (
            <div><div className="font-medium text-emerald-700">Stärken</div>
              <ul className="list-disc pl-5 text-slate-600">{fb.strengths.map((s, i) => <li key={i}>{s}</li>)}</ul></div>
          )}
          {fb.corrections?.length > 0 && (
            <div><div className="font-medium text-rose-700">Korrekturen</div>
              <ul className="list-disc pl-5 text-slate-600">
                {fb.corrections.map((c, i) => <li key={i}><s>{c.original}</s> → <strong>{c.correction}</strong> — {c.explanation}</li>)}
              </ul></div>
          )}
          {fb.correctedText && type === 'Writing' && (
            <div><div className="font-medium text-slate-700">Korrigierte Version</div>
              <p className="rounded bg-white p-2 text-slate-700 ring-1 ring-slate-200">{fb.correctedText}</p></div>
          )}
          {fb.pronunciationTips?.length > 0 && (
            <div><div className="font-medium text-slate-700">Aussprache-Tipps</div>
              <ul className="list-disc pl-5 text-slate-600">{fb.pronunciationTips.map((t, i) => <li key={i}>{t}</li>)}</ul></div>
          )}
        </div>
      )}
    </div>
  )
}

// ---------------- response helpers ----------------

function initialResponse(type, content) {
  if (CHOICE_TYPES.includes(type)) return { selectedIndex: null }
  switch (type) {
    case 'FillInBlank':
    case 'Cloze': return { answers: Array(((content?.text || '').split('___').length - 1)).fill('') }
    case 'Reorder': return { indexes: [] }
    case 'Matching': return { pairs: [] }
    case 'Dictation': return { text: '' }
    case 'Conjugation': return { answer: '' }
    case 'Translation': return { text: '' }
    case 'Writing': return { text: '' }
    case 'Speaking': return { transcript: '' }
    default: return {}
  }
}

function isAnswered(type, r) {
  if (CHOICE_TYPES.includes(type)) return r.selectedIndex !== null
  switch (type) {
    case 'FillInBlank':
    case 'Cloze': return r.answers.some((a) => a && a.trim())
    case 'Reorder': return r.indexes.length > 0
    case 'Matching': return r.pairs.length > 0
    case 'Dictation': return r.text.trim().length > 0
    case 'Conjugation': return r.answer.trim().length > 0
    case 'Translation': return r.text.trim().length > 0
    case 'Writing': return r.text.trim().length > 0
    case 'Speaking': return r.transcript.trim().length > 0
    default: return true
  }
}

// Reorder tracks token indexes for the UI; the grader wants the token strings.
function toPayload(type, response, content) {
  if (type === 'Reorder') {
    return { tokens: response.indexes.map((i) => content.tokens[i]) }
  }
  return response
}

function formatAnswer(a) {
  if (Array.isArray(a)) return a.join(', ')
  return String(a)
}
