import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get } from '../api/client'
import { Spinner, ProgressBar, LevelPill, Alert } from '../components/ui'

export default function Levels() {
  const [levels, setLevels] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => { get('/api/levels').then(setLevels).catch((e) => setError(e.message)) }, [])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!levels) return <Spinner />

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold text-slate-800">Kurse A1 – C1</h1>
        <p className="text-slate-500">Jedes Niveau ist als 2-Wochen-Kurs aufgebaut. Wähle dein Niveau und leg los.</p>
      </div>
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {levels.map((l) => (
          <Link key={l.id} to={`/levels/${l.id}`} className="card transition hover:shadow-md">
            <div className="flex items-center justify-between">
              <LevelPill code={l.code} />
              <span className="text-xs text-slate-400">{l.goetheExam}</span>
            </div>
            <div className="mt-2 text-lg font-semibold text-slate-800">{l.title}</div>
            <p className="mt-1 text-sm text-slate-500">{l.description}</p>
            <div className="mt-3 grid grid-cols-3 gap-2 text-center text-xs text-slate-500">
              <div><div className="text-base font-bold text-slate-700">{l.unitCount}</div>Einheiten</div>
              <div><div className="text-base font-bold text-slate-700">{l.lessonCount}</div>Lektionen</div>
              <div><div className="text-base font-bold text-slate-700">{l.vocabularyCount}</div>Vokabeln</div>
            </div>
            <div className="mt-3 flex items-center gap-2">
              <ProgressBar value={l.progressPercent} />
              <span className="text-xs font-medium text-slate-500">{l.progressPercent}%</span>
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
