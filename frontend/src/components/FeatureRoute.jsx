import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

// Blocks a route when its feature flag is disabled (direct-URL access falls back to home).
export default function FeatureRoute({ flag, children }) {
  const { isFeatureOn } = useAuth()
  if (!isFeatureOn(flag)) return <Navigate to="/" replace />
  return children
}
