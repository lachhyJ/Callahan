import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { loadActiveWorkout, onActiveWorkoutChange } from './activeWorkout'
import { BackIcon, DashboardIcon, PlayIcon, WorkoutIcon } from './icons'
import LoginPage from './pages/LoginPage'
import WorkoutTemplatesPage from './pages/WorkoutTemplatesPage'
import ActiveWorkoutPage from './pages/ActiveWorkoutPage'
import LogWorkoutPage from './pages/LogWorkoutPage'
import LogActivityPage from './pages/LogActivityPage'
import HistoryPage from './pages/HistoryPage'
import DashboardPage from './pages/DashboardPage'
import ExerciseDetailPage from './pages/ExerciseDetailPage'
import ExercisesListPage from './pages/ExercisesListPage'
import WorkoutSessionDetailPage from './pages/WorkoutSessionDetailPage'
import MuscleBalancePage from './pages/MuscleBalancePage'
import StreakPage from './pages/StreakPage'
import StreakDetailPage from './pages/StreakDetailPage'
import TrendsPage from './pages/TrendsPage'
import ProgramPage from './pages/ProgramPage'
import TaperPage from './pages/TaperPage'
import RecentlyDeletedPage from './pages/RecentlyDeletedPage'
import PlateCalculatorPage from './pages/PlateCalculatorPage'
import './App.css'

const TABS = [
  { to: '/', label: 'Workout', Icon: WorkoutIcon },
  { to: '/dashboard', label: 'Dashboard', Icon: DashboardIcon },
]

// Routes reached by drilling down from somewhere else, rather than the
// bottom tabs or a top-level action — these get a Back button in the top
// bar. Everything else (Workout, Dashboard, Login, an active workout, the
// two logging forms) has its own way out already.
const BACK_LINK_ROUTES = ['/history', '/exercises', '/muscle-balance', '/streaks', '/trends', '/program', '/recently-deleted', '/plate-calculator']

function showsBackLink(pathname) {
  return BACK_LINK_ROUTES.includes(pathname)
    || pathname.startsWith('/exercises/')
    || pathname.startsWith('/sessions/')
    || pathname.startsWith('/streaks/')
}

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}

function TopBar() {
  const { logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [activeWorkout, setActiveWorkout] = useState(() => loadActiveWorkout())

  useEffect(() => onActiveWorkoutChange(() => setActiveWorkout(loadActiveWorkout())), [])

  const onThatWorkout = activeWorkout && location.pathname === `/workout/${activeWorkout.templateId}`
  const showResume = activeWorkout && !onThatWorkout
  const showBack = showsBackLink(location.pathname)

  function handleLogout() {
    if (window.confirm('Log out?')) logout()
  }

  return (
    <div className={showResume ? 'top-bar' : 'top-bar idle'}>
      <div className="top-bar-left">
        {showBack && (
          <button type="button" className="back-link" onClick={() => navigate(-1)}><BackIcon /> Back</button>
        )}
      </div>
      <div className="top-bar-right">
        {showResume && (
          <NavLink to={`/workout/${activeWorkout.templateId}`} className="resume-link">
            <PlayIcon /> Resume
          </NavLink>
        )}
        <button type="button" className="logout-btn" onClick={handleLogout}>Log out</button>
      </div>
    </div>
  )
}

function BottomTabBar() {
  return (
    <nav className="bottom-tab-bar">
      {TABS.map(({ to, label, Icon }) => (
        <NavLink key={to} to={to} end={to === '/'} className="tab-link">
          <Icon />
          <span>{label}</span>
        </NavLink>
      ))}
    </nav>
  )
}

// The active workout page has its own fixed-bottom rest-timer bar — a second
// fixed-bottom tab bar would stack on top of it. Resume (in the top bar)
// already covers getting back in, so the tab bar just steps aside here.
function isActiveWorkoutRoute(pathname) {
  return pathname.startsWith('/workout/') && pathname !== '/workout/custom'
}

function AppRoutes() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  const showBottomNav = isAuthenticated && !isActiveWorkoutRoute(location.pathname)

  return (
    <>
      {isAuthenticated && <TopBar />}
      <div className={showBottomNav ? 'app-content with-bottom-nav' : 'app-content'}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<ProtectedRoute><WorkoutTemplatesPage /></ProtectedRoute>} />
          <Route path="/workout/custom" element={<ProtectedRoute><LogWorkoutPage /></ProtectedRoute>} />
          <Route path="/workout/:templateId" element={<ProtectedRoute><ActiveWorkoutPage /></ProtectedRoute>} />
          <Route path="/run" element={<ProtectedRoute><LogActivityPage /></ProtectedRoute>} />
          <Route path="/history" element={<ProtectedRoute><HistoryPage /></ProtectedRoute>} />
          <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
          <Route path="/exercises" element={<ProtectedRoute><ExercisesListPage /></ProtectedRoute>} />
          <Route path="/exercises/:exerciseId" element={<ProtectedRoute><ExerciseDetailPage /></ProtectedRoute>} />
          <Route path="/sessions/:sessionId" element={<ProtectedRoute><WorkoutSessionDetailPage /></ProtectedRoute>} />
          <Route path="/muscle-balance" element={<ProtectedRoute><MuscleBalancePage /></ProtectedRoute>} />
          <Route path="/streaks" element={<ProtectedRoute><StreakPage /></ProtectedRoute>} />
          <Route path="/streaks/:type" element={<ProtectedRoute><StreakDetailPage /></ProtectedRoute>} />
          <Route path="/trends" element={<ProtectedRoute><TrendsPage /></ProtectedRoute>} />
          <Route path="/program" element={<ProtectedRoute><ProgramPage /></ProtectedRoute>} />
          <Route path="/taper" element={<ProtectedRoute><TaperPage /></ProtectedRoute>} />
          <Route path="/recently-deleted" element={<ProtectedRoute><RecentlyDeletedPage /></ProtectedRoute>} />
          <Route path="/plate-calculator" element={<ProtectedRoute><PlateCalculatorPage /></ProtectedRoute>} />
        </Routes>
      </div>
      {showBottomNav && <BottomTabBar />}
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
