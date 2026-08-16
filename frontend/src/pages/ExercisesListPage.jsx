import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { getExercises } from '../api/client'

export default function ExercisesListPage() {
  const [exercises, setExercises] = useState(null)
  const [error, setError] = useState(null)
  const [query, setQuery] = useState('')

  useEffect(() => {
    getExercises().then(setExercises).catch((err) => setError(err.message))
  }, [])

  const grouped = useMemo(() => {
    if (!exercises) return []
    const q = query.trim().toLowerCase()
    const filtered = q ? exercises.filter((e) => e.name.toLowerCase().includes(q)) : exercises

    const byCategory = new Map()
    for (const ex of filtered) {
      const list = byCategory.get(ex.category) ?? []
      list.push(ex)
      byCategory.set(ex.category, list)
    }
    return Array.from(byCategory.entries())
  }, [exercises, query])

  return (
    <main className="page">
      <h1>Exercises</h1>

      {error && <p className="error">{error}</p>}
      {!error && exercises === null && <p>Loading exercises…</p>}

      {exercises && (
        <input
          type="text"
          className="exercise-search"
          placeholder="Search exercises…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Search exercises"
        />
      )}

      {exercises && grouped.length === 0 && (
        <div className="empty-state">
          <p>No exercises match "{query}".</p>
          <button type="button" className="custom-workout-link" onClick={() => setQuery('')}>Clear search</button>
        </div>
      )}

      {grouped.map(([category, list]) => (
        <div key={category} className="exercise-list-group">
          <h2 className="exercise-list-category">{category}</h2>
          <div className="exercise-list">
            {list.map((ex) => (
              <Link key={ex.id} to={`/exercises/${ex.id}`} className="exercise-list-item">
                <span>{ex.name}</span>
                {ex.primaryMuscle && <span className="primary-muscle">{ex.primaryMuscle}</span>}
              </Link>
            ))}
          </div>
        </div>
      ))}
    </main>
  )
}
