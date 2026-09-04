# Syncing program changes into Callahan

Callahan has no in-app editor for workout templates, and that's deliberate,
not a gap waiting to be filled. Program changes (what's in Workout 1/2/3,
what sets/reps/rest/tempo each exercise targets) are decisions worth
considering and cross-referencing carefully — the program PDF (or a
conversation working through the change) is the actual authoring surface.
Callahan just needs to end up reflecting the decision once it's made.

So "editing the plan" isn't a feature — it's a short, occasional
conversation: tell Claude what changed, Claude updates the database
directly. This doc exists so that conversation doesn't have to start from
scratch each time.

## Where the source file lives

`ultimate_athlete_program.pdf` is authored and stored in Nextcloud, not the
repo — the app serves a synced copy, mounted read-only at `/app/ProgramDocs`
and pointed at by `ProgramPdf:Path` in the backend's prod env file. The host
directory behind that mount is a personal Nextcloud folder, so it's set per
host in an untracked `.env` (`PROGRAM_DOCS_HOST_PATH` — see `.env.example`)
rather than committed here.

The same folder syncs to the desktop Nextcloud client, so it's readable
locally too; the local path is recorded in the machine-level
`~/.claude/CLAUDE.md` rather than in this repo.

Worth reading directly when a conversation needs the actual program content
(exercise lists, rep ranges, run session protocols) rather than just being
told what changed — read the PDF instead of guessing or asking for a
transcription.

## The trigger

A cron job on the NAS (`check_program_pdf.sh`, in the app's data directory)
checks the program PDF's mtime daily and sends a notification when it has
changed. That's the cue to say "sync the PDF" or describe the change
directly — no need to wait for or rely on the notification if you already
know what changed.

## What actually gets touched

Three tables, all in the SQLite DB on the NAS's data volume (the host side of
the `/app/App_Data` mount in `docker-compose.prod.yml`):

- **`WorkoutTemplates`** — the three program days (Id, Name, SortOrder).
  Renaming one is the only likely edit here; adding/removing a whole
  template is a bigger decision than a sync and would warrant its own
  conversation about scope, not just a data patch.
- **`WorkoutTemplateExercises`** — one row per exercise-slot within a
  template: `ExerciseId`, `ExerciseOrder`, `TargetSets`, `TargetReps`,
  `RestSeconds`, `Tempo` (e.g. `"2:1:X"`, nullable), `Cue` (the persistent
  per-slot note, nullable — see the exercise detail page). This is where
  most real changes land: a new target rep range, a different rest
  window, reordering exercises within a day, swapping which exercise
  fills a slot.
- **`Exercises`** — the exercise catalog (currently ~90 rows: the
  originally curated ones plus everything pulled in from the Hevy
  import). If a program change introduces an exercise that doesn't exist
  yet, it needs a new row here first (`Name`, `Category` — one of Push /
  Pull / Legs / Core / Cardio / Other) before a `WorkoutTemplateExercises`
  row can reference it. Check for an existing near-match first — the Hevy
  import left some naming inconsistencies (e.g. equipment-suffixed names
  like `"Bench Press (Barbell)"` alongside plain `"Bench Press"`) that
  are worth merging into rather than duplicating further.

Nothing about a session that's already been logged changes — sets are
tied to `WorkoutSessions`/`ExerciseSets` by `ExerciseId` directly, not to
the template slot, so editing a template never touches history.

## The procedure

Same pattern used for every direct-DB change so far (the Hevy import,
exercise merges, tempo backfill, notes backfill):

1. Pull a fresh copy of the live DB down locally over SSH
   (`ssh <nas> "sudo cat <data-dir>/callahan.db" > copy.db`).
2. Write the SQL, run it against the **local copy** first.
3. `PRAGMA foreign_key_check` on the copy, plus a spot-check query
   (`SELECT` the changed rows back out, read them, confirm they say what
   they should).
4. Only once that's clean: stop the backend container
   (`sudo docker compose -f docker-compose.prod.yml stop backend` — avoids
   a write race with the running app), apply the same SQL to the real
   file, re-run the foreign-key check, restart the backend.
5. Confirm the API's actually serving the new values
   (`GET /api/workouttemplates/{id}/start`, or just check it in the app).

No migration is needed for ordinary data changes — `Tempo` and `Cue`
already exist as columns. A schema change (a genuinely new field, not
just new values) would need an EF Core migration, which is a different
and more involved conversation than "update this rep range."

## What this doesn't cover

Bigger structural changes — a new training block entirely, restructuring
which exercises group into which day, adding a fourth template — are
program decisions on a different scale than a sync. Worth a proper
conversation about what's actually changing and why before touching the
database, same as any other real feature request.
