import { useRef, useState } from 'react'
import { post } from '../api/client'
import { Alert } from '../components/ui'
import { useAuth } from '../auth/AuthContext'

// A chat with the multi-agent coach. Alongside the reply it surfaces the agent trace (which
// agents ran, which tools they called) and the run metrics, so the orchestration is visible.
export default function StudyCoach() {
  const { profile } = useAuth()
  const level = profile?.targetLevel || null
  const [log, setLog] = useState([])          // {role, text, steps?, metrics?, plan?}
  const [exercise, setExercise] = useState(null)
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const endRef = useRef(null)

  async function send(message, submission) {
    setBusy(true)
    setError('')
    if (message) setLog((l) => [...l, { role: 'user', text: message }])
    try {
      const r = await post('/api/coach/turn', { message: message || '', level, submission })
      setLog((l) => [...l, {
        role: 'coach', text: r.reply, steps: r.steps, plan: r.plan,
        metrics: r.metrics, weakTopics: r.weakTopics, aiUsed: r.aiUsed,
      }])
      setExercise(r.exercise || null)
      setTimeout(() => endRef.current?.scrollIntoView({ behavior: 'smooth' }), 50)
    } catch (e) {
      setError(e.message)
    } finally {
      setBusy(false)
    }
  }

  function submitText(e) {
    e.preventDefault()
    const m = input.trim()
    if (!m || busy) return
    setInput('')
    send(m)
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="card">
        <span className="chip bg-indigo-100 text-indigo-700">🧭 Lern-Coach</span>
        <h1 className="mt-2 text-2xl font-bold text-slate-800">Dein persönlicher Lern-Coach</h1>
        <p className="text-slate-500">
          Stell eine Grammatikfrage, bitte um eine Übung, oder schick deine Antwort — mehrere
          Agenten (Planer, Grammatik, Übung, Bewertung) arbeiten zusammen und merken sich den Verlauf.
        </p>
      </div>

      {log.length === 0 && (
        <div className="flex flex-wrap gap-2">
          {['Warum steht das Verb am Ende nach „weil“?', 'Gib mir eine Übung zum Akkusativ',
            'Erklär mir die Wechselpräpositionen'].map((s) => (
            <button key={s} className="btn-ghost text-sm" disabled={busy} onClick={() => send(s)}>{s}</button>
          ))}
        </div>
      )}

      <div className="space-y-3">
        {log.map((m, i) => (m.role === 'user'
          ? <UserBubble key={i} text={m.text} />
          : <CoachBubble key={i} msg={m} />))}
        <div ref={endRef} />
      </div>

      {exercise && <ExerciseCard exercise={exercise} disabled={busy} onAnswer={(sub) => send('', sub)} />}

      {error && <Alert kind="error">{error}</Alert>}

      <form onSubmit={submitText} className="sticky bottom-3 flex gap-2">
        <input className="input flex-1" value={input} disabled={busy}
          onChange={(e) => setInput(e.target.value)} placeholder="Frag den Coach etwas…" />
        <button className="btn-primary" disabled={busy || !input.trim()}>{busy ? '…' : 'Senden'}</button>
      </form>
    </div>
  )
}

function UserBubble({ text }) {
  return (
    <div className="flex justify-end">
      <div className="max-w-[80%] rounded-2xl rounded-br-sm bg-brand-600 px-4 py-2 text-white">{text}</div>
    </div>
  )
}

function CoachBubble({ msg }) {
  return (
    <div className="flex justify-start">
      <div className="max-w-[85%] space-y-2">
        <div className="whitespace-pre-line rounded-2xl rounded-bl-sm bg-white px-4 py-3 text-slate-700 ring-1 ring-slate-200">
          {msg.text}
        </div>
        {msg.steps?.length > 0 && <AgentTrace steps={msg.steps} plan={msg.plan} />}
        {msg.metrics && <Metrics metrics={msg.metrics} aiUsed={msg.aiUsed} />}
        {msg.weakTopics?.length > 0 && (
          <div className="text-xs text-slate-400">Schwerpunkte: {msg.weakTopics.join(', ')}</div>
        )}
      </div>
    </div>
  )
}

const AGENT_LABEL = {
  planner: 'Planer', grammar_explainer: 'Grammatik', exercise_generator: 'Übung', evaluator: 'Bewertung',
}

function AgentTrace({ steps, plan }) {
  const chain = steps.map((s) => AGENT_LABEL[s.agent] || s.agent)
  return (
    <details className="rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-500 ring-1 ring-slate-200">
      <summary className="cursor-pointer select-none">
        Agenten: {chain.join(' → ')}
      </summary>
      <ul className="mt-2 space-y-1">
        {steps.map((s, i) => (
          <li key={i}>
            <span className="font-medium text-slate-600">{AGENT_LABEL[s.agent] || s.agent}</span>
            {s.tool && <span className="text-slate-400"> · {s.tool}</span>}
            {s.topic && <span className="text-slate-400"> · {s.topic}</span>}
          </li>
        ))}
      </ul>
    </details>
  )
}

function Metrics({ metrics, aiUsed }) {
  return (
    <div className="flex flex-wrap gap-2 text-[11px] text-slate-400">
      <span>⏱ {metrics.latencyMs} ms</span>
      <span>· {aiUsed ? `KI: ${metrics.totalTokens} Tokens` : 'ohne KI'}</span>
      {metrics.promptVersions?.length > 0 && <span>· {metrics.promptVersions.join(', ')}</span>}
      {metrics.tracing && <span>· getrackt</span>}
    </div>
  )
}

function ExerciseCard({ exercise, disabled, onAnswer }) {
  const [picked, setPicked] = useState(null)
  const content = exercise.content || {}
  const options = content.options || []
  return (
    <div className="card space-y-3 ring-1 ring-indigo-200">
      <div className="text-xs font-semibold uppercase tracking-wide text-indigo-500">Übung vom Coach</div>
      <p className="font-medium text-slate-800">{exercise.prompt}</p>
      {content.question && <p className="text-slate-600">{content.question}</p>}
      {options.length > 0 ? (
        <div className="grid gap-2">
          {options.map((opt, i) => (
            <button key={i} type="button" disabled={disabled}
              className={`btn-ghost justify-start text-left ${picked === i ? 'bg-brand-600 text-white' : ''}`}
              onClick={() => setPicked(i)}>
              <span className="mr-2 font-mono text-xs opacity-70">{String.fromCharCode(65 + i)}</span>{opt}
            </button>
          ))}
          <div className="flex justify-end">
            <button className="btn-primary" disabled={disabled || picked === null}
              onClick={() => onAnswer({ selectedIndex: picked })}>Antwort prüfen</button>
          </div>
        </div>
      ) : (
        <FreeText disabled={disabled} onAnswer={onAnswer} />
      )}
    </div>
  )
}

function FreeText({ disabled, onAnswer }) {
  const [text, setText] = useState('')
  return (
    <div className="space-y-2">
      <textarea className="input min-h-[80px]" value={text} disabled={disabled}
        onChange={(e) => setText(e.target.value)} placeholder="Deine Antwort…" />
      <div className="flex justify-end">
        <button className="btn-primary" disabled={disabled || !text.trim()}
          onClick={() => onAnswer({ text })}>Antwort prüfen</button>
      </div>
    </div>
  )
}
