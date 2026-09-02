---
name: js-dead-code-sweep
description: Find unreachable files, unused exports, and dead CSS classes in the frontend using an import-graph resolver rather than grep. Use when asked to hunt dead code, find unused files/exports/components, check what's safe to delete, audit the frontend for cruft, or clean up after a refactor that left orphans behind. Trigger on "dead code", "unused", "is anything still importing", "safe to delete", "orphaned file", "unreferenced CSS".
---

# JS Dead Code Sweep

Internal skill for the Callahan repo. Not for publication.

It exists because the obvious approach fails in a specific, recognisable way:
two shell one-liners (`grep -rn "from '.*/$base'"` plus a `sed` to pull export
names) flagged **all 63 source files** as unimported — including ones
`App.jsx` demonstrably imports — and emitted raw grep lines like
`103:export function FlameIcon(props) {` as if they were symbol names. The
same analysis written as a ~30-line resolver was correct on the first run.

## The rule

Whole-codebase structural questions — "what is unreachable", "what is never
referenced" — need a resolver, not text search. Text search answers *where
does this string appear*; it cannot answer *does this edge exist in the module
graph*. It has no way to know that `./x` means `x.jsx`, that `import { a as b }`
consumes `a` and not `b`, or that a namespace import consumes everything.

**When a grep-based attempt at a structural question returns implausible
results — above all when it flags *everything* — that is the signal to switch
tools, not to refine the regex.** Do not spend a third tool call on the
pattern.

## Pass 1 — the import graph

```bash
node .claude/skills/js-dead-code-sweep/scripts/import-graph.mjs frontend/src frontend/src/main.jsx
```

Verified against this repo on 2026-09-02: reports 0 unreachable files and the
handful of exports used only inside their own module. It resolves `./x` to
`x.js|x.jsx|x/index.js`, records named/default/namespace bindings per target,
BFS's reachability from the entry, and excludes `*.test.*` from the *report*
while still counting test files as importers.

Read the script before extending it — it is deliberately regex-over-source, not
a real parser, so it has known blind spots:

- **Dynamic `import()`** and any specifier built at runtime are invisible. An
  edge that only exists through a lazy route will read as unreachable.
- **Namespace imports** (`import * as x`) mark every export of the target as
  used; a file imported that way is never reported for unused exports.
- `export { a } from './b'` is treated as consuming all of `./b`.

## Pass 2 — dead CSS classes

Grep the JS + HTML corpus for each class defined in the stylesheets. Before
deleting anything this pass flags, **check for template-literal class
construction** — that is the dominant false-positive source here, and this repo
has real instances of it:

```bash
# interpolated class names (16 real instances in this repo as of 2026-09-02)
grep -rn 'className={`' frontend/src | grep '\${'
# class names held in JS lookup tables
grep -rn '_CLASS\b\|CLASS_BY_\|ClassName =' frontend/src
```

A class like `chart-series-${(idx % 6) + 1}`
(`components/SeasonStrengthChart.jsx:200`) or `set-type-${s.setType.toLowerCase()}`
never appears literally in the source, and a literal-string sweep will
confidently report every one of its variants as dead. The second source is a
constant map — `DIRECTION_CLASS`, `RUNNING_SHAPE_CLASS_BY_TYPE_NAME` — where the
class strings live in JS, far from any `className` attribute.

## Every finding is a candidate, not a verdict

Both passes produce *candidates*. Before deleting, for each one:

1. Grep the bare symbol name across `frontend/src` — a hit outside its own
   module means the graph missed an edge (usually dynamic import).
2. Check whether it is a deliberate seam: test hooks (`__resetForTests`),
   public API kept for symmetry, or a helper used only within its own file but
   exported for testability. These are correct findings and wrong deletions.
3. Confirm nothing in `index.html`, `public/`, or the Capacitor iOS shell
   references it — those are outside the module graph entirely.

## Pre-flight, before reporting or deleting

- [ ] The answer came from the resolver, not from grep output.
- [ ] The result is plausible — "everything is dead" means the tool is broken.
- [ ] Each candidate was cross-checked by bare-name grep (step 1 above).
- [ ] Template-literal class construction was checked before any CSS deletion.
- [ ] Findings are presented as candidates with their false-positive class
      named, not as a delete list.
- [ ] `npm run build && npm test` in `frontend/` after any deletion — the graph
      is an approximation and the build is the arbiter.
