import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import Login from './pages/Login'
import Register from './pages/Register'
import ForgotPassword from './pages/ForgotPassword'
import ResetPassword from './pages/ResetPassword'
import Onboarding from './pages/Onboarding'
import Dashboard from './pages/Dashboard'
import Levels from './pages/Levels'
import LevelDetail from './pages/LevelDetail'
import UnitDetail from './pages/UnitDetail'
import Lesson from './pages/Lesson'
import Vocabulary from './pages/Vocabulary'
import PracticeSet from './pages/PracticeSet'
import StudyPlan from './pages/StudyPlan'
import Profile from './pages/Profile'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ResetPassword />} />
      <Route path="/onboarding" element={<ProtectedRoute><Onboarding /></ProtectedRoute>} />

      <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
        <Route index element={<Dashboard />} />
        <Route path="levels" element={<Levels />} />
        <Route path="levels/:id" element={<LevelDetail />} />
        <Route path="units/:id" element={<UnitDetail />} />
        <Route path="lessons/:id" element={<Lesson />} />
        <Route path="vocabulary" element={<Vocabulary />} />
        <Route path="practice-sets/:id" element={<PracticeSet />} />
        <Route path="study-plan" element={<StudyPlan />} />
        <Route path="profile" element={<Profile />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
