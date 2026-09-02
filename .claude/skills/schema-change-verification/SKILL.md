---
name: schema-change-verification
description: Verify a generated EF Core migration actually contains the operations it should, before building on it or committing. Use when scaffolding or reviewing a migration, adding an entity or column, or when a table appears empty/missing at runtime despite a migration having deployed. Also covers checking what signal remains when a write path is deliberately made non-fatal. Trigger on "add migration", "dotnet ef", "schema change", "new table", "why is this table empty", "--no-build".
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

## Pre-flight

- [ ] No skip-build flag was passed to the scaffolder.
- [ ] The generated migration file was **opened and read**, not just listed.
- [ ] `Up` contains the expected operations; `Down` is a real inverse.
- [ ] Column types checked against SQLite affinity intent (REAL for decimals).
- [ ] `AppDbContextModelSnapshot.cs` updated.
- [ ] Any new swallowed-exception path logs at error level and does not return
      a success status.
- [ ] Migration applied against a scratch copy of the DB and the table/column
      confirmed to exist — never first applied on the NAS.
