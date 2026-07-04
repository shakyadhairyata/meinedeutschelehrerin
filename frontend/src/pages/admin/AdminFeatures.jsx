import { useEffect, useState } from 'react'
import { get, put } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { Spinner, Alert } from '../../components/ui'

export default function AdminFeatures() {
  const { refreshFeatures } = useAuth()
  const [flags, setFlags] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState('')

  useEffect(() => {
    get('/api/admin/features').then(setFlags).catch((e) => setError(e.message))
  }, [])

  async function toggle(flag) {
    setBusy(flag.key)
    try {
      const updated = await put(`/api/admin/features/${flag.key}`, { enabled: !flag.enabled })
      setFlags((fs) => fs.map((f) => (f.key === flag.key ? updated : f)))
      await refreshFeatures()
    } catch (e) { setError(e.message) }
    finally { setBusy('') }
  }

  if (error) return <Alert kind="error">{error}</Alert>
  if (!flags) return <Spinner />

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <div className="text-xs font-bold uppercase tracking-wide text-brand-600">Admin</div>
        <h1 className="text-2xl font-bold text-slate-800">Funktionen</h1>
        <p className="text-slate-500">Schalte Features für alle Nutzer ein oder aus.</p>
      </div>
      <div className="space-y-3">
        {flags.map((f) => (
          <div key={f.key} className="card flex items-center justify-between gap-4">
            <div>
              <div className="font-semibold text-slate-800">{f.key}</div>
              <div className="text-sm text-slate-500">{f.description}</div>
            </div>
            <button type="button" disabled={busy === f.key} onClick={() => toggle(f)}
              aria-pressed={f.enabled} title={f.enabled ? 'An' : 'Aus'}
              className={`relative h-7 w-12 shrink-0 rounded-full transition ${f.enabled ? 'bg-brand-500' : 'bg-slate-400'} disabled:opacity-50`}>
              <span className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-all ${f.enabled ? 'left-6' : 'left-1'}`} />
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}
