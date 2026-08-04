import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import LoginPage from './pages/LoginPage'
import LogWorkoutPage from './pages/LogWorkoutPage'
import LogRunPage from './pages/LogRunPage'
import HistoryPage from './pages/HistoryPage'
import './App.css'

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}

function Nav() {
  const { logout } = useAuth()
  return (
    <nav>
      <NavLink to="/">Log workout</NavLink>
      <NavLink to="/run">Log run</NavLink>
      <NavLink to="/history">History</NavLink>
      <button type="button" onClick={logout}>Log out</button>
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
        <Route path="/" element={<ProtectedRoute><LogWorkoutPage /></ProtectedRoute>} />
        <Route path="/run" element={<ProtectedRoute><LogRunPage /></ProtectedRoute>} />
        <Route path="/history" element={<ProtectedRoute><HistoryPage /></ProtectedRoute>} />
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
