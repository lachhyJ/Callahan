# Triage runbook

Run this by telling a Claude Code session in this repo: "run the triage runbook."

Source folder: `~/moxie-vault/30-projects/callahan/triage/` (mirrors to the
Obsidian iOS vault — new items get dropped there from the phone, no format
required: a sentence, a rant, a pasted screenshot, whatever). "New" = any
note directly in `triage/`. Once processed, it moves to `triage/reviewed/`
— nothing to type on the phone.

`reviewed/` splits into two subfolders once an item's classification is
resolved:
- `reviewed/pending/` — triaged but not yet acted on: trivial fixes not
  yet applied, or ideas not yet folded into `backlog.md`.
- `reviewed/actioned/` — the trivial fix has landed, or the idea is now a
  tracked line in `backlog.md`, or (for "not worth it") the triage note
  itself is the final verdict with nothing further to do.

A note goes straight to `reviewed/actioned/` at the end of a pass if it's
"not worth it" or gets fixed same-session; otherwise it lands in
`reviewed/pending/` until the fix ships or the backlog line is added, at
which point move it to `actioned/` (no need to wait for the next pass).

Cleanup: once a note is in `actioned/`, it can be deleted (screenshots and
all) — the reasoning has either shipped or is now captured as its own
backlog line, so the triage note is redundant. Keep `pending/` notes
indefinitely; they're the only remaining record of the context/screenshots
behind an unshipped idea.

## Filename discipline

Obsidian's default note name for a pasted-in note with no title is a bare
`Untitled.md` / `Untitled 1.md` / `Untitled 2.md` — several unrelated triage
items end up with these same generic names over time. `reviewed/pending/`
and `reviewed/actioned/` are flat folders shared across every past triage
pass, so a same-named note already sitting there is a real, recurring
collision risk, not a hypothetical one (it happened 2026-09-21: a blind
`mv` of a same-named new note overwrote an older `Untitled.md` already in
`reviewed/pending/`, silently destroying it — recovered only because the
NAS's Syncthing trash-can versioning happened to be on).

- **Before writing `## Triage notes`, rename the note file itself** to a
  short descriptive slug drawn from its own content (e.g. `Untitled.md`
  asking about set-count persistence → `Set count should remember last
  session.md`) if it's still `Untitled*.md` or otherwise generic. Do this
  for every note, not just ones that happen to collide — it also makes
  `reviewed/pending/`'s flat listing scannable instead of a wall of
  `Untitled N.md`.
- **Before moving any note into `reviewed/pending/` or `reviewed/actioned/`,
  list that destination folder and confirm the target filename isn't
  already there.** Never batch multiple notes into a single `mv note1
  note2 ... dest/` — move one at a time, or use `mv -n` (no-clobber) as a
  hard backstop even after the check above, so a missed collision fails
  loudly instead of silently overwriting.
- Images keep their original `IMG_NNNN.png` names and move alongside their
  note — collisions there are far less likely (device-assigned sequential
  numbers rarely repeat) but the same no-clobber discipline still applies.

## Steps, per note directly in `triage/` (not already in `reviewed/`)

0. **Before triaging anything, read all four vault docs**:
   `~/moxie-vault/30-projects/callahan/overview.md`, `decisions.md`,
   `backlog.md`, and the most recent handful of `session-log.md` entries.
   This is a cold, one-shot session with no accumulated context from other
   Callahan threads — skipping this step is how a triage pass ends up
   contradicting an existing decision or re-proposing something already
   settled. Do this once per pass, not once per item.
1. Read the note in full, including any attached images (Obsidian drops
   pasted screenshots into an assets folder next to it — check for
   `![[...]]` embeds and open them).
2. Flesh out the idea in your own words: what's actually being asked for,
   restated clearly. **If anything is ambiguous or underspecified, ask
   Lachlan directly in this session rather than guessing** — this runs
   interactively, so use that. Don't invent scope to fill a gap.
   Two different kinds of unknown come up here, and only one of them
   belongs in the backlog instead of a question right now:
   - **A decision only Lachlan can make** (taste, priority, "do you even
     want this behavior") — always ask in-session, even if the answer
     might be "let me think about it." Never write a design question to
     `backlog.md` as a "needs more info" line when the session had the
     chance to just ask and didn't.
   - **Information the session itself can't get right now** (a diary that
     hasn't been captured yet, a repro that hasn't happened) — this is the
     legitimate "needs more info" case for step 4, nothing to ask.
3. Check feasibility against the real codebase — grep for the relevant
   files/patterns, don't reason from memory of similar features elsewhere.
   Also check `backlog.md` for an existing item covering the same ground
   (exact match, near-duplicate, or a related item this idea should be
   merged into / noted against) and `decisions.md` for anything that already
   settled this question or constrains the approach.
4. Classify the outcome:
   - **trivial** — small enough to just fix, no plan doc needed.
   - **needs a plan** — real design/implementation work; write a plan sketch
     (see below) but do NOT create a `.claude/*.plan.md` yet — that only
     happens when Lachlan is actually about to build it.
   - **needs more info** — only for the "session can't get it right now"
     case from step 2 (unavailable diary/repro/etc). If this is actually a
     decision only Lachlan can make, go back and ask him in-session instead
     of classifying it this way.
   - **not worth it** — say why, briefly. A clear "no" is a valid outcome.
     A prior decision that already rules this out counts as "not worth it,"
     not "needs a plan" — cite the decision.
   - **duplicate** — matches an existing `backlog.md` item; note which one
     and whether this triage note adds anything the existing line doesn't
     (a new detail, a screenshot, a reframing) worth folding in.
5. Append a `## Triage notes` section to the *same* note with:
   `Reviewed: YYYY-MM-DD` on its own line first, then your assessment
   (restated idea, feasibility, rough shape of the fix/feature,
   classification, open questions/answers from step 2).
6. Move the note into `triage/reviewed/pending/` if unresolved, or
   `triage/reviewed/actioned/` if it's "not worth it" or got fixed in the
   same session (see the state-tracking note above).

## After the pass

- Summarize what you triaged (one line per item) back to Lachlan.
- Ask before touching `~/moxie-vault/30-projects/callahan/backlog.md` —
  offer to fold in the trivial/backlog-ready items rather than doing it
  silently (standard vault confirm-before-write rule). When a `pending/`
  item's fix lands or its backlog line is added, move its note to
  `actioned/`.
