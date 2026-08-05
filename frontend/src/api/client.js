const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5080'

export async function apiFetch(path, options = {}) {
  const token = localStorage.getItem('callahan_token')
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  }

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers })

  if (res.status === 401) {
    localStorage.removeItem('callahan_token')
    window.dispatchEvent(new Event('callahan-unauthorized'))
    throw new Error('Not authenticated')
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body.error ?? `Request failed (${res.status})`)
  }

  if (res.status === 204) return null
  return res.json()
}

export function login(username, password) {
  return apiFetch('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  })
}

export function getExercises() {
  return apiFetch('/api/exercises')
}

export function createExercise(name, category) {
  return apiFetch('/api/exercises', {
    method: 'POST',
    body: JSON.stringify({ name, category }),
  })
}

export function getWorkoutSessions() {
  return apiFetch('/api/workoutsessions')
}

export function getWorkoutSession(id) {
  return apiFetch(`/api/workoutsessions/${id}`)
}

export function createWorkoutSession(session) {
  return apiFetch('/api/workoutsessions', {
    method: 'POST',
    body: JSON.stringify(session),
  })
}

export function getWorkoutTemplates() {
  return apiFetch('/api/workouttemplates')
}

export function startWorkoutTemplate(id) {
  return apiFetch(`/api/workouttemplates/${id}/start`)
}

export function getFinishers() {
  return apiFetch('/api/finishers')
}

export function subscribeToPush(subscription) {
  return apiFetch('/api/pushsubscriptions', {
    method: 'POST',
    body: JSON.stringify(subscription),
  })
}

export function scheduleRestTimer(durationSeconds, exerciseName) {
  return apiFetch('/api/resttimer/schedule', {
    method: 'POST',
    body: JSON.stringify({ durationSeconds, exerciseName }),
  })
}

export function cancelRestTimer(timerId) {
  return apiFetch(`/api/resttimer/cancel/${timerId}`, { method: 'POST' })
}

export function getRunningSessions() {
  return apiFetch('/api/runningsessions')
}

export function createRunningSession(session) {
  return apiFetch('/api/runningsessions', {
    method: 'POST',
    body: JSON.stringify(session),
  })
}
