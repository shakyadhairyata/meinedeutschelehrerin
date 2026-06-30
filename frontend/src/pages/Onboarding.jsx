import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Alert } from '../components/ui'

const LEVELS = [
  ['A1', 'Anfänger – erste Wörter & Sätze'],
  ['A2', 'Alltag & einfache Gespräche'],
  ['B1', 'Selbstständig im Alltag & Beruf'],
  ['B2', 'Fließend argumentieren'],
  ['C1', 'Nahezu muttersprachlich'],
]

export default function Onboarding() {
  const { saveProfile } = useAuth()
  const navigate = useNavigate()
  const [displayName, setDisplayName] = useState('')
  const [targetLevel, setTargetLevel] = useState('A1')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try {
      await saveProfile({ displayName: displayName.trim() || null, targetLevel, timeZoneId: 'Europe/Berlin' })
      navigate('/')
    } catch (ex) { setError(ex.message) } finally { setBusy(false) }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <form onSubmit={submit} className="card w-full max-w-lg space-y-5">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Willkommen! 🇩🇪</h1>
          <p className="text-slate-500">Erzähl uns kurz von dir, dann stellen wir deinen Kurs ein.</p>
        </div>
        {error && <Alert kind="error">{error}</Alert>}
        <div>
          <label className="label">Wie sollen wir dich nennen?</label>
          <input className="input" value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Dein Name" />
        </div>
        <div>
          <label className="label">Wo möchtest du starten?</label>
          <div className="grid gap-2">
            {LEVELS.map(([code, desc]) => (
              <button type="button" key={code} onClick={() => setTargetLevel(code)}
                className={`flex items-center gap-3 rounded-lg border p-3 text-left transition ${targetLevel === code ? 'border-brand-500 bg-brand-50' : 'border-slate-200 hover:bg-slate-50'}`}>
                <span className="chip bg-brand-100 font-bold text-brand-700">{code}</span>
                <span className="text-sm text-slate-600">{desc}</span>
              </button>
            ))}
          </div>
        </div>
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Speichern…' : 'Los geht’s →'}</button>
      </form>
    </div>
  )
}
