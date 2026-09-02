#!/usr/bin/env node
// Import-graph reachability + unused-export report for a JS/JSX tree.
//
//   node import-graph.mjs <src-dir> <entry-file>
//   node import-graph.mjs frontend/src frontend/src/main.jsx
//
// Reports (1) files unreachable from the entry point, (2) exported bindings
// never imported anywhere. Both are candidates, not verdicts — see SKILL.md.

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, dirname, resolve, relative } from 'node:path'

const [srcDir, entry] = process.argv.slice(2)
if (!srcDir || !entry) {
  console.error('usage: import-graph.mjs <src-dir> <entry-file>')
  process.exit(1)
}

const EXT = ['.js', '.jsx', '.mjs', '.ts', '.tsx']

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name.startsWith('.')) continue
    const p = join(dir, name)
    if (statSync(p).isDirectory()) walk(p, out)
    else if (EXT.some((e) => name.endsWith(e))) out.push(resolve(p))
  }
  return out
}

// Resolve './x' to x.js | x.jsx | x/index.js ... Returns null for packages.
function resolveSpecifier(spec, fromFile) {
  if (!spec.startsWith('.')) return null
  const base = resolve(dirname(fromFile), spec)
  const tries = [base, ...EXT.map((e) => base + e), ...EXT.map((e) => join(base, 'index' + e))]
  for (const t of tries) {
    try {
      if (statSync(t).isFile()) return t
    } catch {}
  }
  return null
}

const IMPORT_RE = /import\s+(?:([\s\S]*?)\s+from\s+)?['"]([^'"]+)['"]/g
const REEXPORT_RE = /export\s+(?:\*|\{[\s\S]*?\})\s+from\s+['"]([^'"]+)['"]/g

// Parse the clause between `import` and `from` into imported binding names.
function parseBindings(clause) {
  const names = []
  if (!clause) return names
  const braced = clause.match(/\{([\s\S]*?)\}/)
  if (braced) {
    for (const part of braced[1].split(',')) {
      const t = part.trim()
      if (!t) continue
      // `a as b` — the ORIGINAL name is what the target module exported.
      names.push(t.split(/\s+as\s+/)[0].trim())
    }
  }
  const outside = clause.replace(/\{[\s\S]*?\}/, '').replace(/^\s*,|,\s*$/g, '').trim()
  for (const t of outside.split(',')) {
    const n = t.trim()
    if (!n || n === '*') continue
    if (/^\*\s+as\s+/.test(n)) names.push('*')       // namespace import: treat all as used
    else names.push('default')
  }
  return names
}

const EXPORT_RES = [
  /export\s+(?:async\s+)?function\s+([A-Za-z0-9_$]+)/g,
  /export\s+(?:const|let|var|class)\s+([A-Za-z0-9_$]+)/g,
]

const files = walk(resolve(srcDir))
const edges = new Map()        // file -> Set(resolved target)
const importedNames = new Map() // resolved target -> Set(binding name)
const exportedNames = new Map() // file -> Set(export name)

for (const file of files) {
  const src = readFileSync(file, 'utf8')
  const targets = new Set()

  const note = (spec, clause) => {
    const target = resolveSpecifier(spec, file)
    if (!target) return
    targets.add(target)
    if (!importedNames.has(target)) importedNames.set(target, new Set())
    for (const n of parseBindings(clause)) importedNames.get(target).add(n)
  }

  for (const m of src.matchAll(IMPORT_RE)) note(m[2], m[1])
  for (const m of src.matchAll(REEXPORT_RE)) note(m[1], '*')

  edges.set(file, targets)

  const exps = new Set()
  for (const re of EXPORT_RES) for (const m of src.matchAll(re)) exps.add(m[1])
  if (/export\s+default\b/.test(src)) exps.add('default')
  exportedNames.set(file, exps)
}

// BFS reachability from the entry.
const entryPath = resolve(entry)
const reachable = new Set([entryPath])
const queue = [entryPath]
while (queue.length) {
  for (const next of edges.get(queue.shift()) ?? []) {
    if (!reachable.has(next)) { reachable.add(next); queue.push(next) }
  }
}

const rel = (p) => relative(process.cwd(), p)
const isTest = (p) => /\.(test|spec)\.[jt]sx?$/.test(p)

const unreachable = files.filter((f) => !reachable.has(f) && !isTest(f)).sort()
console.log(`## Unreachable from ${rel(entryPath)} (${unreachable.length})`)
for (const f of unreachable) console.log('  ' + rel(f))

const unused = []
for (const f of files) {
  if (isTest(f)) continue
  const imported = importedNames.get(f) ?? new Set()
  if (imported.has('*')) continue // namespace import — can't tell which names are used
  for (const name of exportedNames.get(f) ?? []) {
    if (!imported.has(name)) unused.push(`${rel(f)}  ${name}`)
  }
}
console.log(`\n## Exported but never imported (${unused.length})`)
for (const u of unused.sort()) console.log('  ' + u)
