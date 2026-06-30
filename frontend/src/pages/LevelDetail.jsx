import { useEffect, useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { get, post } from '../api/client'
import { Spinner, ProgressBar, LevelPill, Alert } from '../components/ui'

export default function LevelDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [detail, setDetail] = useState(null)
  const [sets, setSets] = useState([])
  const [error, setError] = useState('')
  const [creating, setCreating] = useState(false)

  useEffect(() => {
    setDetail(null)
    Promise.all([get(`/api/levels/${id}`), get(`/api/levels/${id}/practice-sets`)])
      .then(([d, s]) => { setDetail(d); setSets(s) })
      .catch((e) => setError(e.message))
  }, [id])

  async function createPlan() {
    setCreating(true)
    try {
      const today = new Date().toISOString().slice(0, 10)
      await post('/api/study-plan', { levelId: Number(id), startDate: today, minutesPerDay: detail.level.recommendedMinutesPerDay })
      navigate('/study-plan')
    } catch (e) { setError(e.message) } finally { setCreating(false) }
  }

  if (error) return <Alert kind="error">{error}</Alert>
  if (!detail) return <Spinner />
  const { level, units } = detail

  return (
    <div className="space-y-6">
      <div className="card">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="flex items-center gap-2"><LevelPill code={level.code} /><span className="text-xs text-slate-400">{level.goetheExam}</span></div>
            <h1 className="mt-2 text-2xl font-bold text-slate-800">{level.title}</h1>
            <p className="text-slate-500">{level.description}</p>
          </div>
          <button className="btn-primary" disabled={creating} onClick={createPlan}>
            {creating ? 'Erstelle…' : '📅 2-Wochen-Lernplan erstellen'}
          </button>
        </div>
        <div className="mt-4 flex items-center gap-3">
          <ProgressBar value={level.progressPercent} />
          <span className="text-sm font-medium text-slate-500">{level.progressPercent}% · {level.completedLessons}/{level.lessonCount} Lektionen</span>
        </div>
      </div>

      <section>
        <h2 className="mb-3 text-lg font-semibold text-slate-700">Einheiten ({units.length})</h2>
        <div className="grid gap-3 md:grid-cols-2">
          {units.map((u) => (
            <Link key={u.id} to={`/units/${u.id}`} className="card transition hover:shadow-md">
              <div className="flex items-center justify-between">
                <span className="chip bg-slate-100 text-slate-600">Tag {u.order}</span>
                <span className="text-xs text-slate-400">{u.themeTag}</span>
              </div>
              <div className="mt-2 font-semibold text-slate-800">{u.title}</div>
              <p className="mt-1 text-sm text-slate-500">{u.description}</p>
              <div className="mt-3 flex items-center gap-2">
                <ProgressBar value={u.progressPercent} />
                <span className="text-xs text-slate-500">{u.lessonCount} Lekt.</span>
              </div>
            </Link>
          ))}
        </div>
      </section>

      {sets.length > 0 && (
        <section>
          <h2 className="mb-3 text-lg font-semibold text-slate-700">Übungssets & Prüfungen</h2>
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
            {sets.map((s) => (
              <Link key={s.id} to={`/practice-sets/${s.id}`} className="card transition hover:shadow-md">
                <div className="flex items-center justify-between">
                  <span className={`chip ${s.isExam ? 'bg-rose-100 text-rose-700' : 'bg-emerald-100 text-emerald-700'}`}>
                    {s.isExam ? '📝 Prüfung' : '🎯 Drill'}
                  </span>
                  {s.timeLimitMinutes && <span className="text-xs text-slate-400">{s.timeLimitMinutes} Min</span>}
                </div>
                <div className="mt-2 font-semibold text-slate-800">{s.title}</div>
                <p className="mt-1 text-sm text-slate-500">{s.description}</p>
                <div className="mt-2 text-xs text-slate-400">{s.exerciseCount} Aufgaben</div>
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
