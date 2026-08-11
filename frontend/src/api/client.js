const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8080'

export async function apiFetch(path, options = {}) {
  const token = localStorage.getItem('callahan_token')
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers,
  }

  let res
  try {
    res = await fetch(`${API_BASE}${path}`, { ...options, headers })
  } catch {
    throw new Error(
      navigator.onLine
        ? "Couldn't reach the server. Try again in a moment."
        : "You're offline — reconnect and try again."
    )
  }

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

export function getExerciseHistory(exerciseId, limit = 10, offset = 0) {
  return apiFetch(`/api/exercises/${exerciseId}/history?limit=${limit}&offset=${offset}`)
}

export function getExerciseStats(exerciseId) {
  return apiFetch(`/api/exercises/${exerciseId}/stats`)
}

export function getExerciseCues(exerciseId) {
  return apiFetch(`/api/exercises/${exerciseId}/cues`)
}

export function updateCue(workoutTemplateExerciseId, cue) {
  return apiFetch(`/api/workouttemplates/exercises/${workoutTemplateExerciseId}/cue`, {
    method: 'PUT',
    body: JSON.stringify({ cue }),
  })
}

export function getWorkoutSessions() {
  return apiFetch('/api/workoutsessions')
}

export function getMuscleBalance(startDate, endDate) {
  return apiFetch(`/api/musclegroups/balance?startDate=${startDate}&endDate=${endDate}`)
}

export function getWeeklyVolume(weeks = 8) {
  return apiFetch(`/api/workoutsessions/weekly-volume?weeks=${weeks}`)
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

export function scheduleRestTimer(durationSeconds, exerciseName, targetReps, nextSetNumber, totalSets) {
  return apiFetch('/api/resttimer/schedule', {
    method: 'POST',
    body: JSON.stringify({ durationSeconds, exerciseName, targetReps, nextSetNumber, totalSets }),
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
