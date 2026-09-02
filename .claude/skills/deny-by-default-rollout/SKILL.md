---
name: deny-by-default-rollout
description: Assess the blast radius before changing a framework-level default that alters which status code the API emits — authorization fallback policies, global filters, rate limiters, CORS, middleware ordering, error handlers. Use when adding or tightening a default-deny control, when asked "is this safe to turn on globally", or when a change makes protection implicit rather than per-endpoint. Trigger on "FallbackPolicy", "secure by default", "global filter", "require auth everywhere", "middleware", "why is this returning 401/403".
---

# Deny-by-Default Rollout

Internal skill for the Callahan repo. Not for publication.

## The incident this encodes

Adding an authorization `FallbackPolicy` (`backend/Program.cs:62`) so
controllers are protected by default was correct, and it was verified: an
endpoint without `[AllowAnonymous]` returned 401, with it 200. Both checks
passed. The change still shipped a production regression.

ASP.NET Core applies the fallback policy to requests matching **no endpoint at
all**. Every unknown path under the API prefix started returning 401 instead of
404. That looked harmless until the client was read: the fetch wrapper at
`frontend/src/api/client.js:22` treats any 401 as an expired session — it clears
`callahan_token` and dispatches `callahan-unauthorized`. So a stale frontend
calling a route that no longer existed would now **silently log the athlete
out**. The repo already had a comment elsewhere describing that exact
version-skew scenario and expecting a 404.

It was caught only because a post-deploy check happened to probe a route
expected to 404 and got 401.

## The two things that were missed

**1. The control is defined by what it does when nothing matched — and that is
the case least likely to be tested.** Testing "protected endpoint → 401,
`[AllowAnonymous]` endpoint → 200" exercises the two paths you were thinking
about. It says nothing about the paths you weren't.

**2. Changing which status code a system emits is an API change.** Its blast
radius is wherever a caller *branches* on that code — which is in a different
codebase from the change, and therefore invisible to any test, review, or
build that covers only the changed repo.

## Procedure

### Step 1 — enumerate the non-matching and error paths

Before shipping, list every request class the control now covers that is not a
normal endpoint hit, and state the expected status for each:

- unmatched routes (404 → ?), under each prefix the control applies to
- health and readiness probes
- the error handler / problem-details path
- static file and SPA fallback routes
- OPTIONS preflight
- anything registered before the control in the middleware pipeline

Probe them for real. `curl -s -o /dev/null -w '%{http_code}'` against a running
container beats reasoning about middleware order.

### Step 2 — grep the client for every status code whose meaning changed

For each status the control can now emit, find where callers branch on it:

```bash
grep -rn 'status === 40\|status === 4[0-9][0-9]\|res.status' frontend/src/api/
```

The finding that matters is a **shared wrapper that maps one status onto a
destructive action** — logout, token clear, cache purge, forced reload. That
turns a status-code change into a user-visible bug far from the change. In this
repo that wrapper is `frontend/src/api/client.js`, and 401 is the destructive
one.

If such a mapping exists, either scope the control so it cannot emit that
status on the newly-covered paths, or make the client distinguish the two cases
(e.g. only treat 401 as a session expiry when the response carries the
authentication challenge the real auth path sends).

### Step 3 — verify after deploy, on the non-matching path

The post-deploy check must include a route expected to 404. That single probe
is what caught this one.

## Pre-flight

- [ ] Non-matching / error paths enumerated *and probed*, not reasoned about.
- [ ] Client grepped for branches on every status code whose meaning changed.
- [ ] Any status→destructive-action mapping in a shared wrapper identified and
      addressed.
- [ ] Version skew considered explicitly: what does a *stale* client, calling a
      route that no longer exists, now experience?
- [ ] Post-deploy check probes a path that should 404, not just one that should
      401 and one that should 200.
