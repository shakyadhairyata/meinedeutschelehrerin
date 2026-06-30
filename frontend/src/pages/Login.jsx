import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import AuthShell from '../components/AuthShell'
import { Alert } from '../components/ui'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try { await login(email.trim(), password); navigate('/') }
    catch (ex) { setError(ex.message) }
    finally { setBusy(false) }
  }

  return (
    <AuthShell title="Willkommen zurück" subtitle="Melde dich an, um weiterzulernen."
      footer={<>Noch kein Konto? <Link className="font-semibold text-brand-700" to="/register">Registrieren</Link></>}>
      <form onSubmit={submit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <div>
          <label className="label">E-Mail</label>
          <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="du@beispiel.de" />
        </div>
        <div>
          <div className="flex items-center justify-between">
            <label className="label">Passwort</label>
            <Link className="text-xs font-medium text-brand-700 hover:underline" to="/forgot-password">Passwort vergessen?</Link>
          </div>
          <input className="input" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" />
        </div>
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Anmelden…' : 'Anmelden'}</button>
      </form>
    </AuthShell>
  )
}
