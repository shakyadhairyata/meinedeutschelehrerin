import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import AuthShell from '../components/AuthShell'
import { Alert } from '../components/ui'

export default function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try { await register(email.trim(), password); navigate('/onboarding') }
    catch (ex) { setError(ex.message) }
    finally { setBusy(false) }
  }

  return (
    <AuthShell title="Konto erstellen" subtitle="Kostenlos starten und Fortschritt speichern."
      footer={<>Schon registriert? <Link className="font-semibold text-brand-700" to="/login">Anmelden</Link></>}>
      <form onSubmit={submit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <div>
          <label className="label">E-Mail</label>
          <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="du@beispiel.de" />
        </div>
        <div>
          <label className="label">Passwort</label>
          <input className="input" type="password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} placeholder="mind. 8 Zeichen" />
          <p className="mt-1 text-xs text-slate-400">Mind. 8 Zeichen, mit Groß-/Kleinbuchstabe und Ziffer.</p>
        </div>
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Konto wird erstellt…' : 'Registrieren'}</button>
      </form>
    </AuthShell>
  )
}
