import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const NAV = [
  { to: '/', label: 'Dashboard', end: true, icon: '🏠' },
  { to: '/levels', label: 'Kurse', icon: '📚' },
  { to: '/vocabulary', label: 'Vokabeln', icon: '🗂️' },
  { to: '/study-plan', label: 'Lernplan', icon: '🗓️' },
]

export function Brand({ className = '' }) {
  return (
    <span className={`flex items-center gap-2 font-display text-lg font-bold ${className}`}>
      <span className="grid h-9 w-9 place-items-center rounded-xl text-lg shadow-glow"
        style={{ backgroundImage: 'linear-gradient(135deg,#10d96a,#10b981)' }}>👩‍🏫</span>
      <span className="text-white">Meine<span className="text-brand-400">Deutsche</span>Lehrerin</span>
    </span>
  )
}

export default function Layout() {
  const { profile, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-10 border-b border-white/10 bg-ink-900/70 backdrop-blur-lg">
        <div className="mx-auto flex max-w-6xl items-center gap-6 px-4 py-3">
          <NavLink to="/"><Brand /></NavLink>
          <nav className="hidden gap-1 md:flex">
            {NAV.map((n) => (
              <NavLink key={n.to} to={n.to} end={n.end}
                className={({ isActive }) =>
                  `flex items-center gap-1.5 rounded-xl px-3 py-1.5 text-sm font-bold transition ${
                    isActive ? 'bg-brand-500/15 text-brand-300 ring-1 ring-brand-400/30' : 'text-white/70 hover:bg-white/5 hover:text-white'}`}>
                <span>{n.icon}</span>{n.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-3">
            {profile?.currentStreak > 0 && (
              <span className="chip bg-orange-400/20 text-orange-300 ring-1 ring-orange-400/30" title="Aktuelle Lernsträhne">
                🔥 {profile.currentStreak}
              </span>
            )}
            <button onClick={() => navigate('/profile')} className="text-sm font-bold text-white/80 hover:text-brand-300">
              {profile?.displayName || 'Profil'}
            </button>
            <button onClick={() => { logout(); navigate('/login') }} className="btn-ghost px-3 py-1.5 text-xs">
              Abmelden
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
