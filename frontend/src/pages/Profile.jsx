import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { Alert, Stat } from '../components/ui'

const LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1']

export default function Profile() {
  const { profile, saveProfile } = useAuth()
  const [displayName, setDisplayName] = useState(profile?.displayName || '')
  const [targetLevel, setTargetLevel] = useState(profile?.targetLevel || '')
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setSaved(false); setError('')
    try {
      await saveProfile({ displayName, targetLevel: targetLevel || null, timeZoneId: profile.timeZoneId })
      setSaved(true)
    } catch (ex) { setError(ex.message) } finally { setBusy(false) }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-bold text-slate-800">Profil</h1>

      <div className="grid grid-cols-3 gap-4">
        <Stat label="Aktuelle Strähne" value={`🔥 ${profile.currentStreak}`} />
        <Stat label="Längste Strähne" value={profile.longestStreak} />
        <Stat label="Mitglied seit" value={new Date(profile.createdAt).toLocaleDateString('de-DE')} />
      </div>

      <form onSubmit={submit} className="card space-y-4">
        {saved && <Alert kind="success">Profil gespeichert.</Alert>}
        {error && <Alert kind="error">{error}</Alert>}
        <div>
          <label className="label">E-Mail</label>
          <input className="input bg-slate-50" value={profile.email} disabled />
        </div>
        <div>
          <label className="label">Anzeigename</label>
          <input className="input" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </div>
        <div>
          <label className="label">Zielniveau</label>
          <select className="input" value={targetLevel} onChange={(e) => setTargetLevel(e.target.value)}>
            <option value="">— kein Ziel gesetzt —</option>
            {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
          </select>
        </div>
        <button className="btn-primary" disabled={busy}>{busy ? 'Speichern…' : 'Speichern'}</button>
      </form>
    </div>
  )
}
