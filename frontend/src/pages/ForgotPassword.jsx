import { useState } from 'react'
import { Link } from 'react-router-dom'
import { BASE } from '../api/client'
import AuthShell from '../components/AuthShell'
import { Alert } from '../components/ui'

export default function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    setBusy(true); setError('')
    try {
      const res = await fetch(`${BASE}/api/auth/forgotPassword`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim() }),
      })
      if (!res.ok) throw new Error('Anfrage fehlgeschlagen. Bitte später erneut versuchen.')
      setSent(true)
    } catch (ex) { setError(ex.message) } finally { setBusy(false) }
  }

  return (
    <AuthShell title="Passwort vergessen?" subtitle="Wir senden dir einen Code zum Zurücksetzen."
      footer={<>Zurück zur <Link className="font-semibold text-brand-700" to="/login">Anmeldung</Link></>}>
      {sent ? (
        <div className="space-y-4">
          <Alert kind="success">
            Falls ein Konto mit dieser E-Mail existiert, haben wir einen Code gesendet.
          </Alert>
          <Link className="btn-primary w-full" to="/reset-password">Code eingeben & Passwort zurücksetzen</Link>
        </div>
      ) : (
        <form onSubmit={submit} className="space-y-4">
          {error && <Alert kind="error">{error}</Alert>}
          <div>
            <label className="label">E-Mail</label>
            <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="du@beispiel.de" />
          </div>
          <button className="btn-primary w-full" disabled={busy}>{busy ? 'Senden…' : 'Code senden'}</button>
        </form>
      )}
    </AuthShell>
  )
}
