# Design Brief — Callahan

## 1. Product purpose
Tracks gym and running sessions against a structured training program, self-hosted to replace a paywalled free tier (Hevy).

## 2. Primary user
Lachlan himself — logging sets one-handed on his phone mid-workout at the gym between reps, reviewing history later.

**Voice:** Direct and terse — gym vocabulary assumed (reps, sets, kg, no glossary), no hype or exclamation marks, no coach persona. Buttons name the exact action ("Save workout", not "Submit"). Errors and empty states speak plainly, never apologetic. Every string should read like the same person wrote it, because it is.

## 3. Principles (conflict order)
1. **Only ticked is truth.** A set saves only if explicitly ticked complete — typed-but-unconfirmed values are discarded on Finish. *Resolves:* whether draft/partial input should ever persist as logged history. It shouldn't.
2. **Speed at the barbell, not the screen.** Secondary controls (unit toggle, rest-duration presets, per-exercise notes) stay hidden until focused, never sit as persistent chrome during an active set. *Resolves:* the kg/lb live-conversion preview getting cut twice — it moved the UI mid-input and that's worse than the info it added.
3. **One true session, no duplicates.** The app always knows the single in-progress workout and routes there — it never lets two parallel sessions exist to choose between. *Resolves:* "Start a workout" redirecting straight to the existing in-progress session instead of letting a second one start on top.
4. **Built for one user, not many.** Every screen assumes it's already Lachlan, already authenticated behind the Cloudflare gate — no account switching, roles, or management chrome. *Resolves:* keeping auth/account UI to a single login gate, nothing more.

## 4. Success metric
Completes an entire workout, start to finish, one-handed on a phone between sets — and after any interruption (lock screen, app switch, notification) resumes exactly where it left off with zero re-entry of already-logged data.

## 5. Out of scope
- No multiple user accounts, roles, or admin/management UI — one credential, one person
- No numeric load/training prescriptions from readiness data — plain-language framing only ("more tired than usual"), never "reduce load by X%"
- No ad-hoc custom exercise creation — deliberately deferred, stick to the seeded exercise library
- No social features — no feed, following, leaderboards, or sharing
- No nutrition/diet tracking

## 6. Learned constraints
- **2026-08-05** — No live inline value-conversion previews (e.g. "≈ X kg") near active input fields. *Why:* tried twice in different positions during weight-entry; both times it made the UI "move a bit weird" mid-input. Reveal-on-focus controls are fine — a preview that shifts layout as you type is not.
- ~~**2026-08-06** — Rest-timer signature detail deferred~~ — built same day: a depleting progress bar (not a ring — chosen over one to match the app's restrained, non-decorative visual language) on the rest-bar's top edge, anchored left/draining right-to-left, plus a pulsing dot on whichever exercise card has the active timer. `/finalize` no longer needs to flag this.
