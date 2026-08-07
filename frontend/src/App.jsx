import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { loadActiveWorkout, onActiveWorkoutChange } from './activeWorkout'
import { PlayIcon } from './icons'
import LoginPage from './pages/LoginPage'
import WorkoutTemplatesPage from './pages/WorkoutTemplatesPage'
import ActiveWorkoutPage from './pages/ActiveWorkoutPage'
import LogWorkoutPage from './pages/LogWorkoutPage'
import LogRunPage from './pages/LogRunPage'
import HistoryPage from './pages/HistoryPage'
import ExerciseDetailPage from './pages/ExerciseDetailPage'
import './App.css'

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}

function Nav() {
  const { logout } = useAuth()
  const location = useLocation()
  const [activeWorkout, setActiveWorkout] = useState(() => loadActiveWorkout())

  useEffect(() => onActiveWorkoutChange(() => setActiveWorkout(loadActiveWorkout())), [])

  const onThatWorkout = activeWorkout && location.pathname === `/workout/${activeWorkout.templateId}`

  return (
    <nav>
      <NavLink to="/">Workout</NavLink>
      <NavLink to="/run">Log run</NavLink>
      <NavLink to="/history">History</NavLink>
      {activeWorkout && !onThatWorkout && (
        <NavLink to={`/workout/${activeWorkout.templateId}`} className="resume-link">
          <PlayIcon /> Resume
        </NavLink>
      )}
      <button type="button" className="logout-btn" onClick={logout}>Log out</button>
    </nav>
  )
}

function AppRoutes() {
  const { isAuthenticated } = useAuth()
  return (
    <>
      {isAuthenticated && <Nav />}
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<ProtectedRoute><WorkoutTemplatesPage /></ProtectedRoute>} />
        <Route path="/workout/custom" element={<ProtectedRoute><LogWorkoutPage /></ProtectedRoute>} />
        <Route path="/workout/:templateId" element={<ProtectedRoute><ActiveWorkoutPage /></ProtectedRoute>} />
        <Route path="/run" element={<ProtectedRoute><LogRunPage /></ProtectedRoute>} />
        <Route path="/history" element={<ProtectedRoute><HistoryPage /></ProtectedRoute>} />
        <Route path="/exercises/:exerciseId" element={<ProtectedRoute><ExerciseDetailPage /></ProtectedRoute>} />
      </Routes>
    </>
  )
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
