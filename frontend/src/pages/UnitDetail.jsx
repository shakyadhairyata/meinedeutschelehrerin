import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { get } from '../api/client'
import { Spinner, SkillBadge, Alert } from '../components/ui'

const STATUS = {
  Completed: { icon: '✓', cls: 'text-emerald-600' },
  InProgress: { icon: '◐', cls: 'text-amber-500' },
  NotStarted: { icon: '○', cls: 'text-slate-300' },
}

export default function UnitDetail() {
  const { id } = useParams()
  const [unit, setUnit] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => { setUnit(null); get(`/api/units/${id}`).then(setUnit).catch((e) => setError(e.message)) }, [id])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!unit) return <Spinner />

  return (
    <div className="space-y-5">
      <div>
        <span className="text-xs text-slate-400">{unit.themeTag}</span>
        <h1 className="text-2xl font-bold text-slate-800">{unit.title}</h1>
        <p className="text-slate-500">{unit.description}</p>
      </div>
      <div className="space-y-2">
        {unit.lessons.map((l) => {
          const st = STATUS[l.status] || STATUS.NotStarted
          return (
            <Link key={l.id} to={`/lessons/${l.id}`}
              className="card flex items-center gap-4 transition hover:shadow-md">
              <span className={`text-xl ${st.cls}`}>{st.icon}</span>
              <div className="flex-1">
                <div className="font-semibold text-slate-800">{l.title}</div>
                <div className="mt-1 flex items-center gap-2">
                  <SkillBadge skill={l.skill} />
                  <span className="text-xs text-slate-400">{l.exerciseCount} Übungen · ~{l.estimatedMinutes} Min</span>
                </div>
              </div>
              {l.status === 'Completed' && <span className="text-sm font-medium text-emerald-600">{l.scorePercent}%</span>}
              <span className="text-slate-300">→</span>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
