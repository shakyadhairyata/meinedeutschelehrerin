import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { BASE } from '../api/client'
import AuthShell from '../components/AuthShell'
import { Alert } from '../components/ui'

export default function ResetPassword() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [email, setEmail] = useState(params.get('email') || '')
  const [resetCode, setResetCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try {
      const res = await fetch(`${BASE}/api/auth/resetPassword`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim(), resetCode: resetCode.trim(), newPassword }),
      })
      if (!res.ok) {
        let msg = 'Zurücksetzen fehlgeschlagen. Prüfe Code und Passwort.'
        try { const e2 = await res.json(); if (e2.errors) msg = Object.values(e2.errors).flat().join(' ') } catch { /* ignore */ }
        throw new Error(msg)
      }
      navigate('/login', { replace: true })
    } catch (ex) { setError(ex.message) } finally { setBusy(false) }
  }

  return (
    <AuthShell title="Neues Passwort setzen" subtitle="Gib den Code aus der E-Mail und ein neues Passwort ein."
      footer={<>Zurück zur <Link className="font-semibold text-brand-700" to="/login">Anmeldung</Link></>}>
      <form onSubmit={submit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <div>
          <label className="label">E-Mail</label>
          <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="du@beispiel.de" />
        </div>
        <div>
          <label className="label">Code</label>
          <input className="input" required value={resetCode} onChange={(e) => setResetCode(e.target.value)} placeholder="Code aus der E-Mail" />
        </div>
        <div>
          <label className="label">Neues Passwort</label>
          <input className="input" type="password" required minLength={8} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} placeholder="mind. 8 Zeichen" />
        </div>
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Speichern…' : 'Passwort zurücksetzen'}</button>
      </form>
    </AuthShell>
  )
}
