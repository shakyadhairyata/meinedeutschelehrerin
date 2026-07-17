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

  useEffect(() => {
    get(`/api/practice-sets/${id}`).then(setData).catch((e) => setError(e.message))
  }, [id])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!data) return <Spinner />

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <button onClick={() => navigate(-1)} className="text-sm text-slate-500 hover:text-brand-700">← Zurück</button>
      {data.modules?.length > 0
        ? <ModularExam set={data.set} modules={data.modules} />
        : <FlatSet set={data.set} exercises={data.exercises} />}
    </div>
  )
}

function fmt(s) {
  const m = Math.floor(s / 60)
  return `${m}:${String(s % 60).padStart(2, '0')}`
}

function ExamHeader({ set }) {
  return (
    <div className="card">
      <span className="chip bg-rose-100 text-rose-700">📝 Goethe-Prüfung</span>
      <h1 className="mt-2 text-2xl font-bold text-slate-800">{set.title}</h1>
      <p className="text-slate-500">{set.description}</p>
    </div>
  )
}

// A full mock exam: separately-timed modules (Lesen/Hören/Schreiben/Sprechen).
function ModularExam({ set, modules }) {
  const [started, setStarted] = useState(false)
  const [mi, setMi] = useState(0)        // module index
  const [step, setStep] = useState(0)    // exercise within the module
  const [results, setResults] = useState([])
  const [left, setLeft] = useState(0)    // seconds remaining in this module
  const finished = mi >= modules.length

  function nextModule() { setMi((i) => i + 1); setStep(0) }

  // (Re)start the countdown each time we enter a module; auto-advance when time runs out.
  useEffect(() => {
    if (!started || finished) return
    const deadline = Date.now() + modules[mi].timeLimitMinutes * 60 * 1000
    setLeft(modules[mi].timeLimitMinutes * 60)
    const t = setInterval(() => {
      const rem = Math.max(0, Math.round((deadline - Date.now()) / 1000))
      setLeft(rem)
      if (rem <= 0) { clearInterval(t); nextModule() }
    }, 500)
    return () => clearInterval(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [started, mi])

  if (!started) {
    const totalMin = modules.reduce((a, m) => a + m.timeLimitMinutes, 0)
    return (
      <>
        <ExamHeader set={set} />
        <div className="card space-y-3">
          <p className="text-sm text-slate-500">
            {modules.length} Module, jeweils mit eigenem Zeitlimit — {totalMin} Minuten gesamt. Jedes Modul startet seinen Countdown automatisch.
          </p>
          <div className="space-y-2">
            {modules.map((m, i) => (
              <div key={m.id} className="flex items-center justify-between rounded-lg bg-slate-100 px-3 py-2">
                <span className="font-medium text-slate-700">{i + 1}. {m.title}</span>
                <span className="text-sm text-slate-500">{m.exercises.length} Aufgaben · {m.timeLimitMinutes} Min</span>
              </div>
            ))}
          </div>
          <button className="btn-primary w-full" onClick={() => { setStarted(true); setMi(0); setStep(0); setResults([]) }}>
            Prüfung starten
          </button>
        </div>
      </>
    )
  }

  if (finished) {
    const score = results.length ? Math.round(results.reduce((a, r) => a + r.scorePercent, 0) / results.length) : 0
    const passed = score >= 60
    return (
      <div className="card space-y-3 text-center">
        <h2 className="text-xl font-bold text-slate-800">{passed ? 'Bestanden!' : 'Noch nicht bestanden'}</h2>
        <p className="text-slate-500">Gesamtergebnis: <strong>{score}%</strong></p>
        <div className="mx-auto max-w-sm space-y-1 text-left">
          {modules.map((m, i) => {
            const rs = results.filter((r) => r.mi === i)
            const s = rs.length ? Math.round(rs.reduce((a, r) => a + r.scorePercent, 0) / rs.length) : null
            return (
              <div key={m.id} className="flex justify-between text-sm text-slate-600">
                <span>{m.title}</span><span>{s === null ? '—' : `${s}%`}</span>
              </div>
            )
          })}
        </div>
        <button className="btn-primary" onClick={() => { setStarted(false); setMi(0); setStep(0); setResults([]) }}>Nochmal</button>
      </div>
    )
  }

  const module = modules[mi]
  const exs = module.exercises
  const moduleDone = step >= exs.length
  const low = left <= 30

  return (
    <>
      <ExamHeader set={set} />
      <div className="card flex items-center justify-between">
        <div>
          <div className="text-xs font-bold uppercase tracking-wide text-brand-600">Modul {mi + 1} / {modules.length}</div>
          <div className="text-lg font-bold text-slate-800">{module.title}</div>
        </div>
        <div className={`rounded-lg px-3 py-1.5 font-mono text-lg font-bold ${low ? 'bg-rose-100 text-rose-700' : 'bg-slate-100 text-slate-700'}`}>
          ⏱ {fmt(left)}
        </div>
      </div>

      <div className="flex items-center gap-3">
        <ProgressBar value={(Math.min(step, exs.length) / Math.max(exs.length, 1)) * 100} />
        <span className="text-xs text-slate-500">{Math.min(step, exs.length)}/{exs.length}</span>
      </div>

      {!moduleDone ? (
        <ExercisePlayer
          key={`${mi}-${exs[step].id}`}
          exercise={exs[step]}
          index={step}
          total={exs.length}
          onResult={(r) => setResults((prev) => [...prev, { mi, scorePercent: r.scorePercent }])}
          onNext={() => setStep((s) => s + 1)}
        />
      ) : (
        <div className="card text-center">
          <p className="text-slate-600">Modul „{module.title}" abgeschlossen.</p>
          <button className="btn-primary mt-3" onClick={nextModule}>
            {mi + 1 < modules.length ? 'Nächstes Modul →' : 'Prüfung beenden'}
          </button>
        </div>
      )}
    </>
  )
}

// A simple drill / flat set (no modules).
function FlatSet({ set, exercises }) {
  const [step, setStep] = useState(0)
  const [results, setResults] = useState([])
  const total = exercises.length
  const onSummary = step >= total
  const score = results.length ? Math.round(results.reduce((a, r) => a + r.scorePercent, 0) / total) : 0
  const passed = score >= 60

  return (
    <>
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
        <ProgressBar value={(Math.min(step, total) / Math.max(total, 1)) * 100} />
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
          <h2 className="mt-2 text-xl font-bold text-slate-800">{set.isExam ? (passed ? 'Bestanden!' : 'Noch nicht bestanden') : 'Set abgeschlossen'}</h2>
          <p className="mt-1 text-slate-500">Ergebnis: <strong>{score}%</strong></p>
          <button className="btn-primary mt-4" onClick={() => { setStep(0); setResults([]) }}>Nochmal üben</button>
        </div>
      )}
    </>
  )
}
