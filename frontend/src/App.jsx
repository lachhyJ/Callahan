import { useEffect, useState } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5080'

function App() {
  const [health, setHealth] = useState({ state: 'loading' })

  useEffect(() => {
    fetch(`${API_BASE}/api/health`)
      .then((res) => {
        if (!res.ok) throw new Error(`status ${res.status}`)
        return res.json()
      })
      .then((data) => setHealth({ state: 'ok', data }))
      .catch((err) => setHealth({ state: 'error', message: err.message }))
  }, [])

  return (
    <main style={{ fontFamily: 'sans-serif', padding: '2rem' }}>
      <h1>Callahan</h1>
      <p>Backend health check ({API_BASE}/api/health):</p>
      {health.state === 'loading' && <p>Checking…</p>}
      {health.state === 'ok' && (
        <pre>{JSON.stringify(health.data, null, 2)}</pre>
      )}
      {health.state === 'error' && (
        <p style={{ color: 'crimson' }}>Could not reach backend: {health.message}</p>
      )}
    </main>
  )
}

export default App
