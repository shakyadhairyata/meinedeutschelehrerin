import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get } from '../api/client'
import { Spinner, Stat, ProgressBar, SkillBadge, LevelPill, Alert } from '../components/ui'

export default function Dashboard() {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    get('/api/dashboard').then(setData).catch((e) => setError(e.message))
  }, [])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!data) return <Spinner />

  const maxActivity = Math.max(1, ...data.recentActivity.map((a) => a.attempts))

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-800">Hallo, {data.displayName} 👋</h1>
        <p className="text-slate-500">
          {data.targetLevel ? `Zielniveau: ${data.targetLevel}. ` : ''}
          Bleib dran — Konstanz schlägt Intensität.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
        <Stat label="Tage-Strähne" value={`🔥 ${data.currentStreak}`} sub={`Rekord: ${data.longestStreak}`} />
        <Stat label="Lektionen fertig" value={data.lessonsCompleted} />
        <Stat label="Genauigkeit" value={`${data.overallAccuracy}%`} sub={`${data.totalAttempts} Versuche`} />
        <Stat label="Minuten gelernt" value={data.minutesStudied} />
        <Stat label="Vokabeln gemeistert" value={data.vocabularyMastered} />
      </div>

      <section>
        <h2 className="mb-3 text-lg font-semibold text-slate-700">Deine Niveaus</h2>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {data.levels.map((l) => (
            <Link key={l.id} to={`/levels/${l.id}`} className="card transition hover:shadow-md">
              <div className="flex items-center justify-between">
                <LevelPill code={l.code} />
                <span className="text-xs text-slate-400">{l.goetheExam}</span>
              </div>
              <div className="mt-2 font-semibold text-slate-800">{l.title}</div>
              <div className="mt-1 text-xs text-slate-500">
                {l.lessonCount} Lektionen · {l.vocabularyCount} Vokabeln · ~{l.estimatedDays} Tage
              </div>
              <div className="mt-3 flex items-center gap-2">
                <ProgressBar value={l.progressPercent} />
                <span className="text-xs font-medium text-slate-500">{l.progressPercent}%</span>
              </div>
            </Link>
          ))}
        </div>
      </section>

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="card">
          <h2 className="mb-3 text-lg font-semibold text-slate-700">Fertigkeiten</h2>
          {data.skillStats.length === 0 ? (
            <p className="text-sm text-slate-400">Noch keine Daten — mach ein paar Übungen.</p>
          ) : (
            <div className="space-y-3">
              {data.skillStats.map((s) => (
                <div key={s.skill}>
                  <div className="mb-1 flex items-center justify-between text-sm">
                    <SkillBadge skill={s.skill} />
                    <span className="text-slate-500">{s.accuracy}% · {s.attempts} Versuche</span>
                  </div>
                  <ProgressBar value={s.accuracy} />
                </div>
              ))}
            </div>
          )}
        </section>

        <section className="card">
          <h2 className="mb-3 text-lg font-semibold text-slate-700">Schwerpunkte zum Üben</h2>
          {data.topWeaknesses.length === 0 ? (
            <p className="text-sm text-slate-400">Keine Schwächen erkannt. Weiter so! 🎉</p>
          ) : (
            <ul className="space-y-2">
              {data.topWeaknesses.map((w) => (
                <li key={w.grammarTopic} className="flex items-center justify-between rounded-lg bg-rose-50 px-3 py-2">
                  <span className="font-medium text-rose-800">{w.grammarTopic}</span>
                  <span className="text-sm text-rose-600">{w.accuracy}% richtig ({w.attempts}×)</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="card">
        <h2 className="mb-3 text-lg font-semibold text-slate-700">Aktivität (14 Tage)</h2>
        <div className="flex h-28 items-end gap-1">
          {data.recentActivity.map((a) => (
            <div key={a.date} className="flex flex-1 flex-col items-center gap-1" title={`${a.date}: ${a.attempts} Versuche`}>
              <div className="flex w-full items-end" style={{ height: '80px' }}>
                <div className="w-full rounded-t bg-brand-500" style={{ height: `${(a.attempts / maxActivity) * 100}%` }} />
              </div>
              <span className="text-[10px] text-slate-400">{a.date.slice(8, 10)}</span>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
