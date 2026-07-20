import { useState } from 'react'
import { get } from '../api/client'
import { Alert } from './ui'
import { useAuth } from '../auth/AuthContext'

// Grammar explanations retrieved from our own lessons, with the source shown so the learner
// can see which lesson a rule comes from. Retrieval is free; the AI summary is opt-in because
// it is the only part that costs tokens.
export default function GrammarHelp({ query, level }) {
  const { isFeatureOn } = useAuth()
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState('')
  const [error, setError] = useState('')

  if (!isFeatureOn?.('grammar_help') || !query) return null

  async function load(withAnswer) {
    setLoading(withAnswer ? 'answer' : 'sources')
    setError('')
    try {
      const params = new URLSearchParams({ q: query, k: '3' })
      if (level) params.set('level', level)
      if (withAnswer) params.set('answer', 'true')
      setData(await get(`/api/grammar-help?${params.toString()}`))
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading('')
    }
  }

  if (!data) {
    return (
      <div className="mt-3">
        <button className="btn-ghost text-sm" disabled={loading === 'sources'} onClick={() => load(false)}>
          {loading === 'sources' ? 'Suche im Kursmaterial…' : 'Warum ist das falsch?'}
        </button>
        {error && <div className="mt-2"><Alert kind="error">{error}</Alert></div>}
      </div>
    )
  }

  return (
    <div className="mt-3 rounded-lg bg-slate-50 p-3 ring-1 ring-slate-200">
      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
        Grammatik-Hilfe aus deinem Kursmaterial
      </div>

      {data.answer && (
        <p className="mb-3 rounded bg-white p-2 text-sm text-slate-700 ring-1 ring-slate-200">{data.answer}</p>
      )}

      {data.sources.length === 0 ? (
        <p className="text-sm text-slate-500">
          Dazu habe ich im Kursmaterial keine passende Erklärung gefunden.
        </p>
      ) : (
        <ul className="space-y-2">
          {data.sources.map((s, i) => (
            <li key={i} className="text-sm">
              <div className="flex items-center gap-2">
                <span className="chip bg-brand-100 text-brand-700">{s.level}</span>
                <span className="font-medium text-slate-700">{s.title}</span>
                {s.grammarTopic && <span className="text-xs text-slate-400">· {s.grammarTopic}</span>}
              </div>
              <p className="mt-1 whitespace-pre-line text-slate-600">{s.text}</p>
            </li>
          ))}
        </ul>
      )}

      {!data.answer && data.sources.length > 0 && (
        <button className="btn-ghost mt-3 text-sm" disabled={loading === 'answer'} onClick={() => load(true)}>
          {loading === 'answer' ? 'Erkläre…' : 'Kurz zusammenfassen (KI)'}
        </button>
      )}
      {error && <div className="mt-2"><Alert kind="error">{error}</Alert></div>}
    </div>
  )
}
