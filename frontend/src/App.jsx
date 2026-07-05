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
import AdminRoute from './components/AdminRoute'
import AdminLayout from './components/AdminLayout'
import AdminFeatures from './pages/admin/AdminFeatures'
import AdminUsers from './pages/admin/AdminUsers'
import FeatureRoute from './components/FeatureRoute'

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
        <Route path="vocabulary" element={<FeatureRoute flag="vocabulary"><Vocabulary /></FeatureRoute>} />
        <Route path="practice-sets/:id" element={<PracticeSet />} />
        <Route path="study-plan" element={<FeatureRoute flag="study_plan"><StudyPlan /></FeatureRoute>} />
        <Route path="profile" element={<Profile />} />
        <Route path="admin" element={<AdminRoute><AdminLayout /></AdminRoute>}>
          <Route index element={<AdminFeatures />} />
          <Route path="users" element={<AdminUsers />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
