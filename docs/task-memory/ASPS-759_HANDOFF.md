# ASPS-759 — Harden `ScanService.isLocalUrl` loopback guard + `.gitignore` key patterns

**Parent epic:** ASPS-738
**Owner:** browser-extension
**Branch:** `asps-759-harden-islocalurl-gitignore`
**Status:** Implementation complete, pushed to remote. Awaiting QA + code review + security gate (CEO-run). **No PR opened yet** per task instructions.
**Last commit:** `6305935f0bdca1317540264f0cfdbeb32f23df0e` — "ASPS-759 Harden ScanService.isLocalUrl loopback guard + gitignore key patterns"

---

## Summary

Two non-blocking security findings from the ASPS-753 PR #48 gate, both fixed.

### Part 1 — `isLocalUrl()` loopback guard hardened

**File:** [apps/extension/chrome/services/ScanService.js](../../apps/extension/chrome/services/ScanService.js)
- Module-level helpers added above the `ScanService` class (~lines 14-195): `parseIPv4Part`, `parseIPv4ToInt`, `isLoopbackIPv4`, `parseIPv6Groups`, `isLoopbackIPv6`.
- `isLocalUrl(url)` method rewritten (~line 210 onward, inside the class) to use the helpers instead of string-literal matching.

**Canonicalization approach:**
- **IPv4** — a WHATWG-URL-spec-style parser (`parseIPv4Part` / `parseIPv4ToInt`) that accepts 1-4 dot-separated parts, each decimal, `0x`/`0X`-prefixed hex, or leading-zero octal, and combines them into a 32-bit unsigned integer (matching the browser's own IPv4-number-parsing algorithm, so a single decimal like `2130706433` or a partial form like `0x7f.0.0.1` both resolve correctly). `isLoopbackIPv4` then checks the **high byte** (`(ipv4 >>> 24) & 0xff === 127`) for the 127.0.0.0/8 range, or `ipv4 === 0` for any encoding of 0.0.0.0. Checking the parsed high byte (not `startsWith('127.')` on a string) is what keeps `12.7.0.1` correctly classified as non-local — verified by an explicit negative test.
- **IPv6** — `parseIPv6Groups` expands a bracket-stripped IPv6 literal to its 8 16-bit groups, handling `::` compression and an optional embedded IPv4 dotted-quad in the final group (defensive; in practice the runtime's `URL` object already normalizes `::ffff:a.b.c.d` forms to hex-group form before `isLocalUrl` ever sees the string — verified empirically with `node -e` against both Node's URL and confirmed identical via jsdom in the Jest run). `isLoopbackIPv6` treats `0:0:0:0:0:0:0:1` (any valid expansion) as `::1`, and the IPv4-mapped range `::ffff:0:0/96` as loopback when its embedded IPv4 has high byte `127`.
- Note: this relies on `new URL(url).hostname` for the initial hostname extraction (unchanged from before), which itself already canonicalizes decimal/hex/octal IPv4 and IPv6 compression per the WHATWG URL spec in both Chrome's and Node/jsdom's implementations — confirmed by direct `node -e` testing before writing the fix. The custom parsers are still implemented independently (not relying solely on that runtime behavior) per the ticket's explicit ask, so the guard doesn't silently depend on an implementation detail of `URL` that could differ across engines.

**Boundary drawn (documented in code comments directly above the helpers in ScanService.js):**
- IPv4 127.0.0.0/8 (any decimal/hex/octal/dotted encoding) → local.
- IPv4 0.0.0.0 (any encoding) → local (existing behavior, kept).
- IPv6 `::1` (any equivalent expansion) → local.
- IPv6 IPv4-mapped `::ffff:0:0/96` whose embedded IPv4 is in 127.0.0.0/8 → local.
- `localhost` and `localhost.` (single trailing dot) → local. **`*.localhost` subdomains are deliberately NOT covered** — flagged per the ticket's own "note if you add subdomain forms" — not added, to avoid scope creep and because Chrome doesn't route arbitrary `*.localhost` subdomains to loopback the way it does the bare name.
- Unparseable host → **NOT** local (`false`), i.e. same "fail toward scanning it" direction as before (the `catch → false` behavior is preserved for real `new URL()` throws, and the new parsers return `null`/`false` rather than throwing for malformed IPv4/IPv6 literals, which flows through to the same `false` result).
- IPv6 `::` (unspecified address, all-zero) is intentionally **NOT** treated as local — not in the enumerated bypass list, and not a loopback address per RFC 4291.

### Part 2 — `.gitignore`

**File:** [.gitignore](../../.gitignore) — added `*.key`, `*.pem`, `*.pfx` next to the existing CURVE-key ignores.
**Already-tracked check:** `git ls-files | grep -E '\.(key|pem|pfx)$'` → **no matches**. Nothing needed to be flagged or removed.

---

## TDD evidence

**Red** (`apps/extension/chrome/tests/unit/services/ScanService.test.js`, `isLocalUrl` describe block extended):
```
cd apps/extension/chrome/tests
node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/ScanService.test.js
```
Result before the fix: **3 failed, 32 passed, 35 total** — failing cases were `http://[::ffff:127.0.0.1]/`, `http://[::ffff:7f00:1]/`, `http://localhost./` (all expected `true`, got `false`). The decimal/hex/octal IPv4 bypass cases coincidentally already passed under jsdom because `new URL()` pre-normalizes those to dotted-decimal before the old `startsWith('127.')` check ever ran — but the fix does not rely on that; it re-derives the loopback classification from a canonical 32-bit int independent of that behavior.

**Green** (after implementing the helpers + rewriting `isLocalUrl`):
```
cd apps/extension/chrome/tests
node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/ScanService.test.js
```
Result: **35 passed, 35 total.**

**Adjacent regression check:**
```
node --experimental-vm-modules ./node_modules/jest/bin/jest.js unit/services/ScanService.messaging-v1.test.js
```
Result: **2 passed, 2 total.**

**Full extension suite (no regression check):**
```
node --experimental-vm-modules ./node_modules/jest/bin/jest.js
```
Result: **4 failed suites / 13 passed suites, 55 failed / 277 passed, 332 total.**
The 4 failing suites are exactly the 4 quarantined in `scripts/test-baseline-exceptions.json` (untouched by this task): `unit/services/CacheService.test.js` (6), `integration/extension-flow.test.js` (9), `unit/services/IconService.test.js` (29), `unit/services/ConnectionService.test.js` (11) — sum 55, matching exactly. Confirmed via `FAIL`/`PASS` suite listing that `ScanService.test.js` and `ScanService.messaging-v1.test.js` are both fully green and no other suite regressed.

`scripts/check_test_baseline.py` requires per-component report files produced by the full `verify.ps1 -Bootstrap` run (dotnet/analyzer/desktop/extension); not invoked here since this task is extension-only and the manual suite-by-suite comparison against the manifest gave an exact match.

---

## Pre-QA gate status

- [x] Build — no build step for the MV3 extension (`apps/extension/chrome/package.json` is `{"private": true, "type": "module"}`, loaded directly, no bundler).
- [x] Task tests exist and pass (35/35 `ScanService.test.js`, including all Part-1 bypass/negative cases).
- [x] Full component test suite run — no regression versus `scripts/test-baseline-exceptions.json`.
- [x] No uncommitted changes — all 3 changed files committed.
- [x] Merged latest `main` — already up to date (`git fetch && git merge origin/main --no-edit` → "Already up to date").
- [x] Re-ran tests after merge (see final confirmation run: 37/37 across both ScanService suites).
- [x] Pushed to remote — `origin/asps-759-harden-islocalurl-gitignore`.
- [x] This handoff created/updated.
- [x] Spec documents reviewed for impact — see below. **Not edited** (browser-extension does not edit specs).

## Specification documents to flag (not edited — TechWriter/Architect/CEO call)

- None of the indexed spec documents (`ASPS_System_Specification.md`, `DESKTOP_AGENT_FEATURES.md`, `ASPS_System_Overview.md`, `ASPS_DATA_FLOW.md`, messaging spec, WS-AGENT-PROTOCOL.md, ADRs) describe the extension-side `isLocalUrl` loopback-guard logic or its exact bypass list — this is an internal client-side hardening detail of `ScanService.js`, not a documented protocol/contract change. No spec content appears to require updating as a result of this change. Flagging for TechWriter to confirm/decline rather than silently skipping the review step.

## Files changed

- [apps/extension/chrome/services/ScanService.js](../../apps/extension/chrome/services/ScanService.js) — `isLocalUrl` hardening + new module-level canonicalization helpers.
- [apps/extension/chrome/tests/unit/services/ScanService.test.js](../../apps/extension/chrome/tests/unit/services/ScanService.test.js) — extended `isLocalUrl` test matrix (bypass + negative cases).
- [.gitignore](../../.gitignore) — added `*.key`, `*.pem`, `*.pfx`.

## Decisions / notes for reviewers

- No external library used for IP parsing — vanilla JS, matching file style, per ticket constraint.
- Chose module-level pure functions (not class methods) for the parsing helpers since they need no `this` binding and keep `ScanService`'s public API surface unchanged (still just `isLocalUrl(url)`).
- No secrets in the diff; no scope creep — only the two items requested.
