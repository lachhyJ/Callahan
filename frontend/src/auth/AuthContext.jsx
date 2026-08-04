import { createContext, useContext, useEffect, useState } from 'react'
import { login as apiLogin } from '../api/client'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem('callahan_token'))

  useEffect(() => {
    const handleUnauthorized = () => setToken(null)
    window.addEventListener('callahan-unauthorized', handleUnauthorized)
    return () => window.removeEventListener('callahan-unauthorized', handleUnauthorized)
  }, [])

  async function login(username, password) {
    const { token: newToken } = await apiLogin(username, password)
    localStorage.setItem('callahan_token', newToken)
    setToken(newToken)
  }

  function logout() {
    localStorage.removeItem('callahan_token')
    setToken(null)
  }

  return (
    <AuthContext.Provider value={{ isAuthenticated: !!token, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
