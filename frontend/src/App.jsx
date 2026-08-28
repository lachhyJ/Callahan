import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { loadActiveWorkout, onActiveWorkoutChange } from './activeWorkout'
import { clearRestTimer, loadRestTimer, onRestTimerChange } from './restTimer'
import { playBeep } from './beep'
import { getHealth } from './api/client'
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
import UltimateGameDetailPage from './pages/UltimateGameDetailPage'
import GamesListPage from './pages/GamesListPage'
import TournamentDetailPage from './pages/TournamentDetailPage'
import MuscleBalancePage from './pages/MuscleBalancePage'
import StreakPage from './pages/StreakPage'
import StreakDetailPage from './pages/StreakDetailPage'
import TrendsPage from './pages/TrendsPage'
import ProgramPage from './pages/ProgramPage'
import TaperPage from './pages/TaperPage'
import WellnessPage from './pages/WellnessPage'
import RecentlyDeletedPage from './pages/RecentlyDeletedPage'
import PlateCalculatorPage from './pages/PlateCalculatorPage'
import ReportsPage from './pages/ReportsPage'
import ReportDetailPage from './pages/ReportDetailPage'
import { getMonthlyReports } from './api/client'
import './App.css'

const DASHBOARD_TAB = { to: '/dashboard', label: 'Dashboard', Icon: DashboardIcon }

// Routes reached by drilling down from somewhere else, rather than the
// bottom tabs or a top-level action — these get a Back button in the top
// bar. Everything else (Workout, Dashboard, Login, an active workout, the
// two logging forms) has its own way out already.
const BACK_LINK_ROUTES = ['/history', '/exercises', '/muscle-balance', '/streaks', '/trends', '/program', '/recently-deleted', '/plate-calculator', '/reports', '/wellness']

function showsBackLink(pathname) {
  return BACK_LINK_ROUTES.includes(pathname)
    || pathname.startsWith('/exercises/')
    || pathname.startsWith('/sessions/')
    || pathname.startsWith('/streaks/')
    || pathname.startsWith('/reports/')
    || pathname.startsWith('/activities/')
    || pathname.startsWith('/tournaments/')
}

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
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

  function handleBack() {
    // history.state.idx is react-router's own index into the session
    // history stack. idx === 0 means there's nothing before us in *this*
    // session's history, so navigate(-1) (bare browser back) would walk off
    // the top of the stack onto whatever document the webview happens to
    // have one entry back — which after a PWA resume can be a stale
    // bfcached page from before a deploy. Route to an explicit parent
    // instead of popping the document in that case.
    if (window.history.state?.idx === 0) {
      navigate('/dashboard')
    } else {
      navigate(-1)
    }
  }

  return (
    <div className={showResume ? 'top-bar' : 'top-bar idle'}>
      <div className="top-bar-left">
        {showBack && (
          <button type="button" className="back-link" onClick={handleBack}><BackIcon /> Back</button>
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
  const [activeWorkout, setActiveWorkout] = useState(() => loadActiveWorkout())

  useEffect(() => onActiveWorkoutChange(() => setActiveWorkout(loadActiveWorkout())), [])

  const workoutTab = activeWorkout
    ? { to: `/workout/${activeWorkout.templateId}`, label: 'Workout', Icon: WorkoutIcon, end: true }
    : { to: '/', label: 'Workout', Icon: WorkoutIcon, end: true }
  const tabs = [workoutTab, { ...DASHBOARD_TAB, end: false }]

  return (
    <nav className="bottom-tab-bar">
      {tabs.map(({ to, label, Icon, end }) => (
        <NavLink key={label} to={to} end={end} className="tab-link">
          <Icon />
          <span>{label}</span>
        </NavLink>
      ))}
    </nav>
  )
}

// Persistent rest-timer countdown shown on every page except the matching
// active workout page itself, which already renders its own full rest bar
// and owns the tick/expiry/beep logic while mounted. This bar takes over
// that responsibility the moment the athlete navigates away, so the
// countdown (and the alert when it hits zero) still happens if they've gone
// to check the dashboard or program mid-rest.
function useGlobalRestTimer() {
  const location = useLocation()
  const [restTimer, setRestTimer] = useState(() => loadRestTimer())
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => onRestTimerChange(() => setRestTimer(loadRestTimer())), [])

  const onActiveWorkoutPage = restTimer && location.pathname === `/workout/${restTimer.templateId}`
  const isTicking = restTimer && !onActiveWorkoutPage

  useEffect(() => {
    if (!isTicking) return
    const interval = setInterval(() => setNow(Date.now()), 1000)
    const onVisible = () => {
      if (document.visibilityState === 'visible') setNow(Date.now())
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [isTicking])

  useEffect(() => {
    if (!isTicking) return
    const remaining = Math.round((restTimer.endAt - now) / 1000)
    if (remaining <= 0) {
      playBeep()
      clearRestTimer()
      setRestTimer(null)
    }
  }, [now, isTicking, restTimer])

  return { restTimer, isTicking, now }
}

function GlobalRestBar({ restTimer, isTicking, now }) {
  const navigate = useNavigate()

  if (!isTicking) return null

  const remainingSeconds = Math.max(0, Math.round((restTimer.endAt - now) / 1000))

  return (
    <button type="button" className="global-rest-bar" onClick={() => navigate(`/workout/${restTimer.templateId}`)}>
      <span className="resting-dot" />
      <span className="global-rest-bar-label">Resting — {restTimer.exerciseName}</span>
      <span className="rest-countdown-mini">{formatCountdown(remainingSeconds)}</span>
    </button>
  )
}

function AppRoutes() {
  const { isAuthenticated } = useAuth()
  const { restTimer, isTicking, now } = useGlobalRestTimer()
  const showBottomNav = isAuthenticated
  const contentClassName = ['app-content', showBottomNav && 'with-bottom-nav', isTicking && 'with-rest-bar']
    .filter(Boolean).join(' ')

  return (
    <>
      {isAuthenticated && <TopBar />}
      <div className={contentClassName}>
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
          <Route path="/activities/:activityId" element={<ProtectedRoute><UltimateGameDetailPage /></ProtectedRoute>} />
          <Route path="/games" element={<ProtectedRoute><GamesListPage /></ProtectedRoute>} />
          <Route path="/tournaments/:tournamentId" element={<ProtectedRoute><TournamentDetailPage /></ProtectedRoute>} />
          <Route path="/muscle-balance" element={<ProtectedRoute><MuscleBalancePage /></ProtectedRoute>} />
          <Route path="/streaks" element={<ProtectedRoute><StreakPage /></ProtectedRoute>} />
          <Route path="/streaks/:type" element={<ProtectedRoute><StreakDetailPage /></ProtectedRoute>} />
          <Route path="/trends" element={<ProtectedRoute><TrendsPage /></ProtectedRoute>} />
          <Route path="/program" element={<ProtectedRoute><ProgramPage /></ProtectedRoute>} />
          <Route path="/taper" element={<ProtectedRoute><TaperPage /></ProtectedRoute>} />
          <Route path="/wellness" element={<ProtectedRoute><WellnessPage /></ProtectedRoute>} />
          <Route path="/recently-deleted" element={<ProtectedRoute><RecentlyDeletedPage /></ProtectedRoute>} />
          <Route path="/plate-calculator" element={<ProtectedRoute><PlateCalculatorPage /></ProtectedRoute>} />
          <Route path="/reports" element={<ProtectedRoute><ReportsPage /></ProtectedRoute>} />
          <Route path="/reports/:year/:month" element={<ProtectedRoute><ReportDetailPage /></ProtectedRoute>} />
        </Routes>
      </div>
      {isAuthenticated && <GlobalRestBar restTimer={restTimer} isTicking={isTicking} now={now} />}
      {showBottomNav && <BottomTabBar />}
    </>
  )
}

// Self-heal for stale bundles (see the app-scoped back-button fix above,
// which this complements): the backend's build version is a fresh GUID
// per process start, so it changes on every deploy. If it doesn't match
// what this tab last saw, the bundle it's running was built before the
// current backend and may call routes that no longer exist — reload once
// to pick up the current one instead of surfacing a 404 screen.
function useStaleBundleSelfHeal() {
  useEffect(() => {
    getHealth()
      .then(({ version }) => {
        const stored = localStorage.getItem('callahan_build_version')
        localStorage.setItem('callahan_build_version', version)
        if (stored && stored !== version) window.location.reload()
      })
      .catch(() => {})
  }, [])
}

function App() {
  useStaleBundleSelfHeal()
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}

export default App
