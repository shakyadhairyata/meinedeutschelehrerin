import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getAuth } from '../api/client'
import { Spinner } from './ui'

export default function ProtectedRoute({ children }) {
  const { profile, loading } = useAuth()
  if (loading) return <Spinner />
  if (!getAuth()?.accessToken || !profile) return <Navigate to="/login" replace />
  return children
}
