// Whether the exercise picker should offer to create what's been typed.
//
// The catalog has no unique constraint on name, so this predicate is the only
// thing stopping a second "Bench Press" being created from a search that just
// hadn't matched yet. Case- and whitespace-insensitive on purpose: "bench
// press" and "Bench Press " are the same exercise to a person, and a duplicate
// splits that exercise's history in two permanently.
export function shouldOfferCreate(query, catalog) {
  const typed = (query ?? '').trim()
  if (typed.length === 0) return false
  const needle = typed.toLowerCase()
  return !(catalog ?? []).some((e) => e.name.trim().toLowerCase() === needle)
}
