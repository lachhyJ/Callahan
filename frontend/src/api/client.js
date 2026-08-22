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

export function updateExerciseAssisted(exerciseId, isAssisted) {
  return apiFetch(`/api/exercises/${exerciseId}/assisted`, {
    method: 'PUT',
    body: JSON.stringify({ isAssisted }),
  })
}

export function updateExerciseName(exerciseId, name) {
  return apiFetch(`/api/exercises/${exerciseId}/name`, {
    method: 'PUT',
    body: JSON.stringify({ name }),
  })
}

export function getWorkoutSessions({ start, end } = {}) {
  const params = new URLSearchParams()
  if (start) params.set('start', start)
  if (end) params.set('end', end)
  const query = params.toString()
  return apiFetch(`/api/workoutsessions${query ? `?${query}` : ''}`)
}

export function getMuscleBalance(startDate, endDate) {
  return apiFetch(`/api/musclegroups/balance?startDate=${startDate}&endDate=${endDate}`)
}

export function getWeeklyVolume(weeks = 8) {
  return apiFetch(`/api/workoutsessions/weekly-volume?weeks=${weeks}`)
}

export function getStreaks() {
  return apiFetch('/api/streaks')
}

export function getStreakDetail(type) {
  return apiFetch(`/api/streaks/${type}`)
}

export function getTrends(months = 6) {
  return apiFetch(`/api/trends?months=${months}`)
}

export function getLiftTrends(months = 6) {
  return apiFetch(`/api/trends/exercises?months=${months}`)
}

export function getRunTypeTrends(months = 6) {
  return apiFetch(`/api/trends/runs?months=${months}`)
}

export function getTaperEvents() {
  return apiFetch('/api/taper/events')
}

export function getTaperRecommendation() {
  return apiFetch('/api/taper/recommendation')
}

export function createTaperEvent({ date, name, taperDays }) {
  return apiFetch('/api/taper/events', {
    method: 'POST',
    body: JSON.stringify({ date, name: name || null, taperDays }),
  })
}

export function deleteTaperEvent(id) {
  return apiFetch(`/api/taper/events/${id}`, { method: 'DELETE' })
}

export function getTaperCheckIns(eventId) {
  return apiFetch(`/api/taper/events/${eventId}/checkins`)
}

export function upsertTaperCheckIn(eventId, { date, energy, soreness, motivation, context }) {
  return apiFetch(`/api/taper/events/${eventId}/checkins`, {
    method: 'PUT',
    body: JSON.stringify({ date, energy, soreness, motivation, context: context || null }),
  })
}

export function getTaperConsult(eventId, question) {
  return apiFetch(`/api/taper/events/${eventId}/consult`, {
    method: 'POST',
    body: JSON.stringify({ question }),
  })
}

// Not apiFetch: that always parses JSON, and a PDF has to come back as a
// blob so it can be handed to an <iframe> via an object URL — a plain
// <iframe src> can't carry the Bearer token.
export async function getProgramPdfBlob() {
  const token = localStorage.getItem('callahan_token')
  const res = await fetch(`${API_BASE}/api/program/pdf`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (res.status === 401) {
    localStorage.removeItem('callahan_token')
    window.dispatchEvent(new Event('callahan-unauthorized'))
    throw new Error('Not authenticated')
  }
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body.error ?? `Request failed (${res.status})`)
  }
  return res.blob()
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

export function updateWorkoutSessionName(id, name) {
  return apiFetch(`/api/workoutsessions/${id}/name`, {
    method: 'PUT',
    body: JSON.stringify({ name }),
  })
}

export function deleteWorkoutSession(id) {
  return apiFetch(`/api/workoutsessions/${id}`, { method: 'DELETE' })
}

export function restoreWorkoutSession(id) {
  return apiFetch(`/api/workoutsessions/${id}/restore`, { method: 'POST' })
}

export function getDeletedWorkoutSessions() {
  return apiFetch('/api/workoutsessions/deleted')
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

export function getActivities({ start, end } = {}) {
  const params = new URLSearchParams()
  if (start) params.set('start', start)
  if (end) params.set('end', end)
  const query = params.toString()
  return apiFetch(`/api/activities${query ? `?${query}` : ''}`)
}

export function createActivity(activity) {
  return apiFetch('/api/activities', {
    method: 'POST',
    body: JSON.stringify(activity),
  })
}

export function getRunSessionTypes() {
  return apiFetch('/api/runsessiontypes')
}

export function deleteActivity(id) {
  return apiFetch(`/api/activities/${id}`, { method: 'DELETE' })
}

export function restoreActivity(id) {
  return apiFetch(`/api/activities/${id}/restore`, { method: 'POST' })
}

export function getDeletedActivities() {
  return apiFetch('/api/activities/deleted')
}

export function updateActivityRunSessionType(id, runSessionTypeId) {
  return apiFetch(`/api/activities/${id}/run-session-type`, {
    method: 'PUT',
    body: JSON.stringify({ runSessionTypeId }),
  })
}

export function getHealth() {
  return apiFetch('/api/health')
}
