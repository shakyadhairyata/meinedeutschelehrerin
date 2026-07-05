import { NavLink, Outlet } from 'react-router-dom'

const TABS = [
  { to: '/admin', label: 'Funktionen', end: true },
  { to: '/admin/users', label: 'Nutzer', end: false },
]

export default function AdminLayout() {
  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <div className="text-xs font-bold uppercase tracking-wide text-amber-500">Admin</div>
        <h1 className="text-2xl font-bold text-slate-800">Verwaltung</h1>
      </div>
      <div className="flex gap-2 border-b border-white/10 pb-2">
        {TABS.map((t) => (
          <NavLink key={t.to} to={t.to} end={t.end}
            className={({ isActive }) =>
              `rounded-lg px-3 py-1.5 text-sm font-bold transition ${
                isActive ? 'bg-brand-500/15 text-brand-300' : 'text-white/60 hover:text-white'}`}>
            {t.label}
          </NavLink>
        ))}
      </div>
      <Outlet />
    </div>
  )
}
