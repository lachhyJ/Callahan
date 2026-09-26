# Garmin watch app token refresh

The Garmin watch app (the Connect IQ rest-timer data field in `watch/`)
authenticates with a 30-day JWT, per the v0 auth design in
`.claude/plans/garmin-rest-timer-data-field.plan.md` (2.2).

**As sideloaded (unsigned), the token can't be pasted in at runtime** —
that was the original v0 design, but it turned out not to be reachable in
practice: Garmin Connect Mobile's "My Data Fields" settings screen only
lists Connect IQ Store apps, data fields (unlike standalone apps/widgets)
have no on-device settings screen of their own, and Garmin Express — the
normal fallback settings editor — doesn't support Apple Silicon Macs.
`bake-and-build.sh` below is the actual working path: the token gets
baked into the build itself, and refreshing it means rebuilding and
re-copying the `.PRG` onto the watch.

## One-time setup

Add your Callahan login to macOS Keychain via **Keychain Access.app**
(Spotlight → "Keychain Access") — not the command line, so the password
is never typed into a terminal or shell history:

1. File → New Password Item
2. Keychain Item Name: `callahan-app`
3. Account Name: your Callahan username
4. Password: your Callahan password
5. Add (to the login keychain)

## Usage — refreshing the token (do this monthly)

```bash
./bake-and-build.sh
```

The first run will prompt macOS's own Keychain access dialog — choose
**"Always Allow"** for Terminal so it doesn't ask every time. This:

1. Mints a fresh token from Callahan's own login endpoint.
2. Writes it into `watch/resources/settings/properties.xml` (gitignored
   — see below — never committed).
3. Rebuilds `watch/bin/CallahanDataField.prg`.

Then: open **OpenMTP**, connect the watch (USB mode set to **MTP**, not
"Garmin"), and copy the rebuilt `.PRG` into `GARMIN/Apps` on the watch,
replacing the existing one. No Garmin Connect Mobile step needed.

## `properties.xml` is gitignored, not `properties.xml.example`

`properties.xml` holds the real token once baked — it must never be
committed. `properties.xml.example` is the tracked placeholder template;
`bake-and-build.sh` copies it to `properties.xml` automatically on a
fresh clone/worktree if the real file doesn't exist yet.

## `get-token.sh` (clipboard-only) — currently not useful

`get-token.sh` mints a token and copies it to the clipboard, for a
paste-based settings flow. Kept around for if a real on-device or
Garmin-Connect-Mobile settings path is ever found for sideloaded data
fields — right now there's nowhere to paste it, so prefer
`bake-and-build.sh`.

## Why not OAuth

`makeOAuthRequest` (a real Garmin Connect Mobile login flow, no manual
token handling at all) is the long-term upgrade path — see the plan's
2.2 — but it needs actual OAuth server infrastructure on the Callahan
backend that doesn't exist yet (an authorization endpoint, a redirect
handler, a code-for-token exchange), which is a real chunk of work for a
single-user app. Worth it only once the rest-timer data field itself is
proven useful in practice, not before.
