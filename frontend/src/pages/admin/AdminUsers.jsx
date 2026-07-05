import { useEffect, useState } from 'react'
import { get, put } from '../../api/client'
import { Spinner, Alert } from '../../components/ui'

export default function AdminUsers() {
  const [users, setUsers] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState('')

  useEffect(() => {
    get('/api/admin/users').then(setUsers).catch((e) => setError(e.message))
  }, [])

  async function setTier(u, tier) {
    setBusy(u.id)
    try {
      await put(`/api/admin/users/${u.id}/tier`, { tier })
      setUsers((us) => us.map((x) => (x.id === u.id ? { ...x, tier } : x)))
    } catch (e) { setError(e.message) }
    finally { setBusy('') }
  }

  if (error) return <Alert kind="error">{error}</Alert>
  if (!users) return <Spinner />

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-500">
        {users.length} Nutzer. Schalte den <strong>Paid</strong>-Tarif frei, um KI-Feedback zu erlauben.
      </p>
      <div className="space-y-2">
        {users.map((u) => (
          <div key={u.id} className="card flex items-center justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate font-semibold text-slate-800">
                {u.email}
                {u.isAdmin && <span className="chip ml-2 bg-amber-400/20 text-amber-300">Admin</span>}
              </div>
              <div className="text-xs text-slate-500">
                KI heute: {u.aiUsageToday} · seit {new Date(u.createdAt).toLocaleDateString('de-DE')}
              </div>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <span className={`chip ${u.tier === 'Paid' ? 'bg-brand-500/20 text-brand-300' : 'bg-slate-500/20 text-slate-300'}`}>{u.tier}</span>
              {u.tier === 'Paid' ? (
                <button disabled={busy === u.id} className="btn-ghost px-3 py-1.5 text-xs" onClick={() => setTier(u, 'Free')}>Auf Free</button>
              ) : (
                <button disabled={busy === u.id} className="btn-success px-3 py-1.5 text-xs" onClick={() => setTier(u, 'Paid')}>Auf Paid</button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
