import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { get } from '../api/client'
import { Spinner, Alert, ProgressBar } from '../components/ui'
import ExercisePlayer from '../components/ExercisePlayer'

export default function PracticeSet() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [step, setStep] = useState(0)
  const [results, setResults] = useState([])

  useEffect(() => {
    get(`/api/practice-sets/${id}`).then((d) => { setData(d); setStep(0); setResults([]) }).catch((e) => setError(e.message))
  }, [id])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!data) return <Spinner />

  const { set, exercises } = data
  const total = exercises.length
  const onSummary = step >= total
  const score = results.length ? Math.round(results.reduce((a, r) => a + r.scorePercent, 0) / total) : 0
  const passed = score >= 60

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <button onClick={() => navigate(-1)} className="text-sm text-slate-500 hover:text-brand-700">← Zurück</button>
      <div className="card">
        <div className="flex items-center justify-between">
          <span className={`chip ${set.isExam ? 'bg-rose-100 text-rose-700' : 'bg-emerald-100 text-emerald-700'}`}>
            {set.isExam ? '📝 Prüfung' : '🎯 Übungsset'}
          </span>
          {set.timeLimitMinutes && <span className="text-xs text-slate-400">Empfohlen: {set.timeLimitMinutes} Min</span>}
        </div>
        <h1 className="mt-2 text-2xl font-bold text-slate-800">{set.title}</h1>
        <p className="text-slate-500">{set.description}</p>
      </div>

      <div className="flex items-center gap-3">
        <ProgressBar value={(Math.min(step, total) / total) * 100} />
        <span className="text-xs text-slate-500">{Math.min(step, total)}/{total}</span>
      </div>

      {!onSummary ? (
        <ExercisePlayer
          key={exercises[step].id}
          exercise={exercises[step]}
          index={step}
          total={total}
          onResult={(r) => setResults((prev) => [...prev, r])}
          onNext={() => setStep((s) => s + 1)}
        />
      ) : (
        <div className="card text-center">
          <div className="text-4xl">{passed ? '🏆' : '💪'}</div>
          <h2 className="mt-2 text-xl font-bold text-slate-800">{set.isExam ? (passed ? 'Bestanden!' : 'Noch nicht bestanden') : 'Set abgeschlossen'}</h2>
          <p className="mt-1 text-slate-500">Ergebnis: <strong>{score}%</strong></p>
          <button className="btn-primary mt-4" onClick={() => { setStep(0); setResults([]) }}>Nochmal üben</button>
        </div>
      )}
    </div>
  )
}
