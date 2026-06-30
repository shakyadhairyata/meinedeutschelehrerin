import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get, post } from '../api/client'
import { Spinner, ProgressBar, LevelPill, Alert } from '../components/ui'

export default function StudyPlan() {
  const [plan, setPlan] = useState(undefined) // undefined = loading, null = none
  const [levels, setLevels] = useState([])
  const [form, setForm] = useState({ levelId: '', startDate: new Date().toISOString().slice(0, 10), minutesPerDay: 90 })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  function load() {
    Promise.all([get('/api/study-plan'), get('/api/levels')])
      .then(([p, ls]) => { setPlan(p); setLevels(ls); setForm((f) => ({ ...f, levelId: f.levelId || ls[0]?.id })) })
      .catch((e) => setError(e.message))
  }
  useEffect(load, [])

  async function create(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try {
      const p = await post('/api/study-plan', {
        levelId: Number(form.levelId), startDate: form.startDate, minutesPerDay: Number(form.minutesPerDay),
      })
      setPlan(p)
    } catch (ex) { setError(ex.message) } finally { setBusy(false) }
  }

  if (plan === undefined) return error ? <Alert kind="error">{error}</Alert> : <Spinner />

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-800">Dein Lernplan</h1>
        {plan && <button className="btn-ghost" onClick={() => setPlan(null)}>Neuen Plan erstellen</button>}
      </div>
      {error && <Alert kind="error">{error}</Alert>}

      {!plan ? (
        <form onSubmit={create} className="card max-w-lg space-y-4">
          <p className="text-sm text-slate-500">Erstelle einen 2-Wochen-Plan: eine Einheit pro Tag bis zur Prüfung.</p>
          <div>
            <label className="label">Niveau</label>
            <select className="input" value={form.levelId} onChange={(e) => setForm({ ...form, levelId: e.target.value })}>
              {levels.map((l) => <option key={l.id} value={l.id}>{l.code} – {l.title}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Startdatum</label>
              <input className="input" type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
            </div>
            <div>
              <label className="label">Minuten/Tag</label>
              <input className="input" type="number" min="15" step="15" value={form.minutesPerDay} onChange={(e) => setForm({ ...form, minutesPerDay: e.target.value })} />
            </div>
          </div>
          <button className="btn-primary w-full" disabled={busy}>{busy ? 'Erstelle…' : 'Plan erstellen'}</button>
        </form>
      ) : (
        <>
          <div className="card flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2"><LevelPill code={plan.levelCode} /><span className="text-sm text-slate-600">Start: {plan.startDate} · {plan.minutesPerDay} Min/Tag</span></div>
            <span className="text-sm text-slate-500">{plan.days.filter((d) => d.completed).length}/{plan.days.length} Tage erledigt</span>
          </div>
          <div className="space-y-2">
            {plan.days.map((d) => (
              <Link key={d.dayNumber} to={`/units/${d.unitId}`}
                className="card flex items-center gap-4 transition hover:shadow-md">
                <div className={`grid h-10 w-10 shrink-0 place-items-center rounded-full text-sm font-bold ${d.completed ? 'bg-emerald-100 text-emerald-700' : 'bg-brand-50 text-brand-700'}`}>
                  {d.completed ? '✓' : d.dayNumber}
                </div>
                <div className="flex-1">
                  <div className="font-semibold text-slate-800">Tag {d.dayNumber} · {d.unitTitle}</div>
                  <div className="text-xs text-slate-400">{d.date} · {d.themeTag} · {d.targetMinutes} Min</div>
                  <div className="mt-2"><ProgressBar value={d.unitProgressPercent} /></div>
                </div>
                <span className="text-slate-300">→</span>
              </Link>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
