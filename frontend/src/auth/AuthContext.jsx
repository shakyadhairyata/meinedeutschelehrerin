import { createContext, useContext, useEffect, useState } from 'react'
import { BASE, get, post, put, getAuth, setAuth, storeTokens } from '../api/client'

const AuthContext = createContext(null)
export const useAuth = () => useContext(AuthContext)

export function AuthProvider({ children }) {
  const [profile, setProfile] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    (async () => {
      if (getAuth()?.accessToken) {
        try { setProfile(await get('/api/profile')) } catch { setAuth(null) }
      }
      setLoading(false)
    })()
  }, [])

  async function login(email, password) {
    const res = await fetch(`${BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
    if (!res.ok) throw new Error('Anmeldung fehlgeschlagen. Prüfe E-Mail und Passwort.')
    storeTokens(await res.json())
    setProfile(await get('/api/profile'))
  }

  async function register(email, password) {
    const res = await fetch(`${BASE}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
    if (!res.ok) {
      let msg = 'Registrierung fehlgeschlagen.'
      try {
        const e = await res.json()
        if (e.errors) msg = Object.values(e.errors).flat().join(' ')
      } catch { /* ignore */ }
      throw new Error(msg)
    }
    await login(email, password)
  }

  function logout() {
    setAuth(null)
    setProfile(null)
  }

  async function saveProfile(patch) {
    const updated = await put('/api/profile', patch)
    setProfile(updated)
    return updated
  }

  const value = { profile, loading, login, register, logout, saveProfile, refreshProfile: async () => setProfile(await get('/api/profile')) }
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
