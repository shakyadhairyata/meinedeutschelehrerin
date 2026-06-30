// Thin fetch wrapper: attaches the bearer token, transparently refreshes it on 401,
// and surfaces API errors as thrown Error objects.
export const BASE = import.meta.env.VITE_API_BASE_URL || ''
const KEY = 'mdt_auth'

export function getAuth() {
  try { return JSON.parse(localStorage.getItem(KEY)) } catch { return null }
}
export function setAuth(a) {
  if (a) localStorage.setItem(KEY, JSON.stringify(a))
  else localStorage.removeItem(KEY)
}

function authHeader() {
  const a = getAuth()
  return a?.accessToken ? { Authorization: `Bearer ${a.accessToken}` } : {}
}

export function storeTokens(d) {
  setAuth({
    accessToken: d.accessToken,
    refreshToken: d.refreshToken,
    expiresAt: Date.now() + (d.expiresIn ?? 3600) * 1000,
  })
}

async function refresh() {
  const a = getAuth()
  if (!a?.refreshToken) return false
  const res = await fetch(`${BASE}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: a.refreshToken }),
  })
  if (!res.ok) { setAuth(null); return false }
  storeTokens(await res.json())
  return true
}

export async function api(path, { method = 'GET', body, retry = true } = {}) {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...authHeader() },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (res.status === 401 && retry && (await refresh())) {
    return api(path, { method, body, retry: false })
  }
  if (!res.ok) {
    let msg = `${res.status} ${res.statusText}`
    try {
      const e = await res.json()
      msg = e.detail || e.title || (e.errors && JSON.stringify(e.errors)) || msg
    } catch { /* non-JSON error body */ }
    throw new Error(msg)
  }
  if (res.status === 204) return null
  const ct = res.headers.get('content-type') || ''
  return ct.includes('application/json') ? res.json() : res.text()
}

export const get = (p) => api(p)
export const post = (p, body) => api(p, { method: 'POST', body })
export const put = (p, body) => api(p, { method: 'PUT', body })
