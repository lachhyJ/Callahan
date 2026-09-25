# Garmin watch app token refresh

The Garmin watch app (the Connect IQ rest-timer data field in `watch/`)
authenticates with a pasted 30-day JWT, per the v0 auth design in
`.claude/plans/garmin-rest-timer-data-field.plan.md` (2.2). This script
mints a fresh one without needing to log into the Callahan app in a
browser and dig it out of dev tools.

## One-time setup

Add your Callahan login to macOS Keychain via **Keychain Access.app**
(Spotlight → "Keychain Access") — not the command line, so the password
is never typed into a terminal or shell history:

1. File → New Password Item
2. Keychain Item Name: `callahan-app`
3. Account Name: your Callahan username
4. Password: your Callahan password
5. Add (to the login keychain)

## Usage

```bash
./get-token.sh
```

The first run will prompt macOS's own Keychain access dialog — choose
**"Always Allow"** for Terminal so it doesn't ask every time. The token
is copied to your clipboard; paste it into the Garmin watch app's
**Auth token** setting via Garmin Connect Mobile (Watch → Connect IQ
Store icon → My Data Fields → Rest Timer).

## Why not fully automatic

v0's auth is deliberately a pasted token, not OAuth — see the plan's 2.2.
OAuth via `makeOAuthRequest` (a real Garmin Connect Mobile login flow,
no manual paste) is the noted upgrade path once the pasted-token version
is proven working end to end, and would also become a Settings-page
candidate. This script is the practical middle ground until then: one
command, no typed password, no browser dev tools.
