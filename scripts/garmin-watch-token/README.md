# Garmin watch app token refresh

The Garmin watch app (the Connect IQ rest-timer data field in `watch/`)
authenticates with a 30-day JWT minted by Callahan's own login endpoint.

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
2. Writes it, and the server's base URL, into
   `watch/source/GeneratedAuthToken.mc` as compiled constants (gitignored
   — see below — never committed).
3. Rebuilds `watch/bin/CallahanDataField.prg`.

The base URL defaults to the production host; set `CALLAHAN_BASE_URL` to
build against another backend.

Then: open **OpenMTP**, connect the watch (USB mode set to **MTP**, not
"Garmin"), and copy the rebuilt `.PRG` into `GARMIN/Apps` on the watch,
replacing the existing one. No Garmin Connect Mobile step needed.

## `GeneratedAuthToken.mc` is gitignored

It holds the real token once baked, so it must never be committed.
`bake-and-build.sh` writes it from scratch on every run. A build that skips
the script (the simulator straight from `monkey.jungle`) needs the file to
exist: copy `GeneratedAuthToken.mc.example` beside it by hand.

Neither the token nor the URL is a Connect IQ Property. Properties persist
across app updates for the same app id, so a changed default never reaches
an already-installed watch, and a sideloaded data field has no settings
screen to edit one.

## Why not OAuth

`makeOAuthRequest` (a real Garmin Connect Mobile login flow, no manual
token handling at all) is the long-term upgrade path, but it needs actual OAuth server infrastructure on the Callahan
backend that doesn't exist yet (an authorization endpoint, a redirect
handler, a code-for-token exchange), which is a real chunk of work for a
single-user app. Worth it only once the rest-timer data field itself is
proven useful in practice, not before.
