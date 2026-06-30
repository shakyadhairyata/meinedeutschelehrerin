import { useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { get, post } from '../api/client'
import { Spinner, Markdown, SkillBadge, AudioButton, ProgressBar, Alert } from '../components/ui'
import ExercisePlayer from '../components/ExercisePlayer'

export default function Lesson() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [lesson, setLesson] = useState(null)
  const [error, setError] = useState('')
  const [step, setStep] = useState(0) // 0..exercises.length (last = summary)
  const [results, setResults] = useState([])
  const [completed, setCompleted] = useState(false)
  const startRef = useRef(Date.now())

  useEffect(() => {
    get(`/api/lessons/${id}`).then((l) => {
      setLesson(l); setStep(0); setResults([]); setCompleted(false); startRef.current = Date.now()
    }).catch((e) => setError(e.message))
  }, [id])

  if (error) return <Alert kind="error">{error}</Alert>
  if (!lesson) return <Spinner />

  const exercises = lesson.exercises || []
  const total = exercises.length
  const onSummary = step >= total

  async function finish() {
    const timeSpentSeconds = Math.round((Date.now() - startRef.current) / 1000)
    try {
      await post(`/api/lessons/${id}/complete`, { lessonId: Number(id), timeSpentSeconds })
      setCompleted(true)
    } catch (e) { setError(e.message) }
  }

  const avgScore = results.length ? Math.round(results.reduce((a, r) => a + r.scorePercent, 0) / results.length) : 0

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <button onClick={() => navigate(-1)} className="text-sm text-slate-500 hover:text-brand-700">← Zurück</button>

      <div className="card">
        <div className="mb-2 flex items-center justify-between">
          <SkillBadge skill={lesson.skill} />
          {lesson.grammarTopic && <span className="text-xs text-slate-400">Thema: {lesson.grammarTopic}</span>}
        </div>
        <h1 className="text-2xl font-bold text-slate-800">{lesson.title}</h1>
        <div className="mt-3"><Markdown>{lesson.content}</Markdown></div>
        {lesson.audioScript && (
          <div className="mt-3 flex items-center gap-3 rounded-lg bg-emerald-50 p-3">
            <AudioButton text={lesson.audioScript} label="Hörtext abspielen" />
            <span className="text-xs text-emerald-700">Hör so oft du möchtest.</span>
          </div>
        )}
      </div>

      {total > 0 && (
        <div className="flex items-center gap-3">
          <ProgressBar value={(Math.min(step, total) / total) * 100} />
          <span className="text-xs text-slate-500">{Math.min(step, total)}/{total}</span>
        </div>
      )}

      {!onSummary && total > 0 && (
        <ExercisePlayer
          key={exercises[step].id}
          exercise={exercises[step]}
          index={step}
          total={total}
          onResult={(r) => setResults((prev) => [...prev, r])}
          onNext={() => setStep((s) => s + 1)}
        />
      )}

      {(onSummary || total === 0) && (
        <div className="card text-center">
          {total > 0 && (
            <>
              <div className="text-4xl">{avgScore >= 80 ? '🎉' : avgScore >= 50 ? '👍' : '💪'}</div>
              <h2 className="mt-2 text-xl font-bold text-slate-800">Übungen geschafft!</h2>
              <p className="mt-1 text-slate-500">Durchschnitt: <strong>{avgScore}%</strong> ({results.length} Übungen)</p>
            </>
          )}
          {total === 0 && <p className="text-slate-500">Diese Lektion ist zum Lesen — markiere sie als erledigt.</p>}
          {!completed ? (
            <button className="btn-success mt-4" onClick={finish}>Lektion abschließen ✓</button>
          ) : (
            <div className="mt-4 space-y-3">
              <Alert kind="success">Fortschritt gespeichert. Gut gemacht!</Alert>
              <button className="btn-primary" onClick={() => navigate(-1)}>Zur Einheit zurück</button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
