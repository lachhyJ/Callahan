---
name: schema-change-verification
description: Verify a generated EF Core migration actually contains the operations it should, before building on it or committing. Use when scaffolding or reviewing a migration, adding an entity or column, adding rows to a seed-managed (HasData) table, or when a table appears empty/missing at runtime despite a migration having deployed. Also covers checking what signal remains when a write path is deliberately made non-fatal. Trigger on "add migration", "dotnet ef", "schema change", "new table", "HasData", "seed data", "reference table", "primary key collision", "why is this table empty", "--no-build".
---

# Schema Change Verification

Internal skill for the Callahan repo. Not for publication.

Stack: EF Core over SQLite, `backend/Migrations/`, model in `backend/Models/`,
context in `backend/Data/`.

## The incident this encodes

A migration for a new usage-tracking table was scaffolded with `--no-build`
added to save time. The tool read the **last-built assembly**, which did not yet
contain the new entity, detected no model change, and emitted a migration with
**empty `Up` and `Down` methods** — and reported success.

Nothing failed. The build afterwards passed. The tests passed. The migration
would have deployed cleanly to the NAS, created no table, and left every insert
failing at runtime.

It was compounded by a decision made minutes earlier: the endpoint writing to
that table wrapped its insert in a catch that logged a warning and returned
success, on the reasoning that telemetry must never break the app. Together
those two would have produced a permanently empty table reporting HTTP success —
discovered weeks later, with the collection interval unrecoverable.

It was caught only because the next step happened to be reading the generated
file before committing.

## Rule 1 — never pass a skip-build flag to a generator whose input is the compiled model

`dotnet ef migrations add` reads the built assembly. `--no-build` means it reads
a *stale* one. The time it saves is the time it takes to be wrong silently.

This holds for **every** `dotnet ef` subcommand, not just `add`. Scaffolding a
migration leaves the on-disk assembly stale by construction, so the next `ef`
call after it is the one most likely to be wrong:

- `migrations remove --no-build` after a fresh `add` sees the *previous*
  migration as the last one and deletes it, an unrelated, already-committed
  migration, and reverts the snapshot to match. The just-scaffolded files are
  untracked and stay on disk, which hides the damage. Always check the
  `Removing migration 'NAME'` line names the migration you meant. Recovery:
  `git checkout` the deleted migration and `AppDbContextModelSnapshot.cs`,
  delete the stray scaffold, rebuild, retry.
- `database update --no-build` against a scratch DB straight after `add` aborts
  with `PendingModelChangesWarning`: the stale assembly's snapshot predates the
  new column while the entity class compiled into it doesn't.

Run `dotnet build` between `migrations add` and any `ef` command that verifies,
applies or removes it.

## Rule 2 — a generated artefact is unverified until read

After scaffolding, open the migration and confirm it contains the operations you
expected:

```bash
ls -t backend/Migrations/*.cs | head -2
grep -c 'migrationBuilder\.' backend/Migrations/<timestamp>_<Name>.cs
```

Zero `migrationBuilder.` calls in `Up` is the empty-migration signature. Check
specifically that:

- `Up` contains `CreateTable` / `AddColumn` / `AlterColumn` for the change you made
- `Down` is a real inverse, not empty
- the column types match the intent — SQLite affinity is easy to get wrong here;
  this repo deliberately stores decimals as `REAL` (see the
  `StoreDecimalsAsReal` migration), so a new decimal column emitted as `TEXT`
  is a bug, not a default
- `AppDbContextModelSnapshot.cs` was updated in the same scaffold

**Also read it for operations that assert the status quo.** Adding a column to
a `HasData`-seeded entity makes the scaffolder emit one `UpdateData` per seed row
re-stating the new column's default (one T4 change produced 46 of them beside 4
`AddColumn`s). `AddColumn`'s `defaultValue` already sets existing rows, so they
do nothing — except hand seed-management ownership of the column (see Rule 4),
which is actively harmful if the app also writes it. Delete every `UpdateData`
that doesn't change a value, then confirm the snapshot is still in sync with
`dotnet ef migrations has-pending-model-changes` and an `Up` against a scratch DB.

**Code generators report that they ran, not that they produced anything.**
Exit code 0 and "Done." mean the tool completed, not that the artefact is
non-empty.

## Rule 3 — check what signal remains when a write path is made non-fatal

The second half of this incident generalises past migrations. When an error is
deliberately swallowed so it "can never break the app", ask what evidence
survives the failure — especially on a **write path whose output is not read
back until long after the failure**.

"This must never break the app" is an argument for not surfacing failures *to
the user*. It is never an argument for not recording them.

The resolution in `backend/Controllers/UsageController.cs:65` is the pattern to
copy: catch so tracking can't break the app it measures, log at error level,
**and return 500 rather than a success status**, with a comment saying why. The
client ignores the response; the log and the status code do not.

## Rule 4 — a seed-managed table that later takes runtime writes has drifted three ways

`HasData` seeding assumes the framework is the only writer. Once an import or a
user-facing create flow writes to the same table, the seed block becomes a
partial, stale view of production — and the model snapshot diverges in ways that
are invisible from reading either the seed block or the migrations:

1. **Rows** — production has rows the seed block doesn't (import, UI creates).
2. **Columns** — a feature shipped later mutates a column at runtime that the
   seed block still declares a fixed value for.
3. **The ID counter** — the dangerous one. Seeded IDs stop at 30 in the
   snapshot; production has allocated up to 92. A newly seeded row is assigned
   an ID that already exists in production, and the migration **fails on a
   primary-key collision at deploy time** — after passing every local check.

Before adding seeded rows to any table that also accepts runtime writes:

```bash
# against a scratch copy of the prod DB, per seeded table
sqlite3 scratch.db "SELECT MAX(Id), COUNT(*) FROM <Table>;"
```

Compare `MAX(Id)` against the highest ID in the seed block and choose new IDs
above the *production* ceiling, never the seed-block ceiling. Then classify each
drifted column as **seed-owned** (reconcile to the seed block), **runtime-owned**
(remove from seed ownership — reconciling it silently resets the user's tuned
values on the next migration), or **unmanaged**. Never infer the ID ceiling from
the seed block.

## Rule 5 — a data migration needs a fresh-database gate, not only a production-copy gate

A migration that inserts rows *referencing existing rows* (wiring new program
templates to exercise rows, say) has an inverted failure mode: if the referenced
rows arrived via a historical import rather than the seed block, the migration
passes against a copy of production and **fails in every other environment** —
local dev, CI, a rebuilt host — because those are built from the seed block
alone. Production is fine; a new contributor or a disaster rebuild hits the
break. Seen here only because a mistyped connection-string key accidentally
pointed the run at the seeded dev DB.

- Run any row-inserting data migration against **both** a production copy and an
  empty database freshly migrated from the seed block. Both must pass.
- Where a referenced row might be absent, make the insert idempotent —
  `INSERT OR IGNORE`, or a guarded `INSERT ... WHERE NOT EXISTS` — so the
  migration is a no-op where the row exists and a backfill where it doesn't.
- Before trusting that a run targeted the database you meant, echo the resolved
  connection target (or confirm a known-distinct row count) — a silently
  redirected connection string is how the wrong-DB pass happens.

## Pre-flight

- [ ] No skip-build flag was passed to the scaffolder.
- [ ] The generated migration file was **opened and read**, not just listed.
- [ ] `Up` contains the expected operations; `Down` is a real inverse.
- [ ] Column types checked against SQLite affinity intent (REAL for decimals).
- [ ] `AppDbContextModelSnapshot.cs` updated.
- [ ] Any new swallowed-exception path logs at error level and does not return
      a success status.
- [ ] For seeded rows added to a table that also takes runtime writes:
      production `MAX(Id)` was queried and new IDs chosen above it; each
      drifted column classified seed-owned / runtime-owned / unmanaged.
- [ ] A row-inserting data migration was run against both a production copy
      **and** an empty seed-built database; inserts referencing existing rows
      are idempotent; the resolved connection target was echoed before trust.
- [ ] Migration applied against a scratch copy of the DB and the table/column
      confirmed to exist — never first applied on the NAS.
