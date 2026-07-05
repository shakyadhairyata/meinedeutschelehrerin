import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Spinner } from './ui'

export default function AdminRoute({ children }) {
  const { profile, loading } = useAuth()
  if (loading) return <Spinner />
  if (!profile) return <Navigate to="/login" replace />
  if (!profile.isAdmin) return <Navigate to="/" replace />
  return children
}
