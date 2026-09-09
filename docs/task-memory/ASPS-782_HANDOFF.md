# ASPS-782 — Normalize quotes/backslashes in Bash secret-scan tokens

**Task:** ASPS-782 — Close the two inner-quote / backslash-escape evasions left
by ASPS-780 in the Telegram CEO bot's step-1 Bash secret-path guard
(`findSecretPathInBashCommand`). Parent epic **ASPS-738**.

**Branch:** `asps-782-normalize-quote-backslash-secret-scan` (off latest `main`, pushed to origin)
**JIRA status:** In Progress — ready for QA / code review / security gate
**Last updated:** 2026-09-10

---

## The residual (closed)

ASPS-780's `findSecretPathInBashCommand` splits the command on
`BASH_TOKEN_SEPARATOR`, strips SURROUNDING quotes per token, and runs
`matchSecretPath`. Two evasions survived because it never removes INNER quotes
and never unescapes backslashes — the SHELL reads a secret path the token does
not spell:

1. **Inner-quote splitting** — `cat /tmp/x.p"e"m` / `cat /tmp/x.p''em`: shell
   reads `/tmp/x.pem`, but the token keeps the embedded quotes, so `/\.pem$/`
   misses.
2. **Backslash escaping** — `cat /tmp/x.pe\m`: shell reads `/tmp/x.pem`, but `\`
   IS a `BASH_TOKEN_SEPARATOR` member, so the token splits into `/tmp/x.pe` +
   `m` and never matches.

(Vectors where the secret NAME tolerates trailing junk — `id_rsa.p"e"m`,
`ACCESS_KEYS.en\v`, `"/tmp/a".key` — were already caught by ASPS-780 because
those patterns are prefix/suffix-tolerant; only the strict `.pem$`-style suffix
patterns were evadable.)

## What changed

| File | Change |
|---|---|
| `apps/telegram-ceo/src/security.ts` | Added `heuristicShellDequote(command)` (removes ALL quotes; drops escaping backslashes, keeping the next char; drops a lone trailing `\`). `findSecretPathInBashCommand` now loops over `[command, heuristicShellDequote(command)]`, running the SAME tokenize-and-`matchSecretPath` scan over each. Additive — the raw ASPS-780 pass is unchanged and runs first. |
| `apps/telegram-ceo/src/__tests__/security.test.ts` | Added ASPS-782 unit cases to the `findSecretPathInBashCommand` block: deny for the 3 evading vectors + 3 already-caught tolerant vectors, no-false-positive for the legit list (incl. `echo "hello.world"`), and a positive `echo "a.pem"` argument-deny. |
| `apps/telegram-ceo/src/__tests__/agent.test.ts` | Added an ASPS-782 `createCanUseTool` describe block: hard-deny the obfuscated vectors sandboxed (no Telegram approval), legit commands still auto-allow. |

## Normalization approach (and the backslash-as-separator case)

- `heuristicShellDequote` is a **heuristic**, explicitly NOT a shell parser
  (documented in the code): it does not honor real quoting semantics (a `\`
  inside single quotes is literal; `\"` is a literal quote; a quoted `;` is not
  an operator). For the sole purpose of isolating a secret-suffixed path token,
  it deliberately over-collapses — errs toward MORE matches (fail-safe), never
  fewer.
- **Backslash-as-separator** is handled by dequoting the WHOLE command BEFORE
  the `BASH_TOKEN_SEPARATOR` split (not per raw token). `\` is a separator
  member, so `x.pe\m` would be split into `x.pe` + `m` before any per-token
  normalization could run; dequoting the whole string first reconstructs
  `x.pem` as one token, which then matches. This is the "reconstruct" option
  from the ticket.
- **Why keep the raw pass first (no regression):** a Windows-separated
  `C:\Users\x\id_rsa` matches `/(^|[\\/])id_rsa…$/` via the raw pass (backslash
  split) but would dequote to `C:Usersxid_rsa` and MISS. Because the raw pass
  runs first and is untouched, such matches never regress; the dequote pass only
  ever ADDS hits. Reported `field` stays `"command"`.

## Preserved behavior (untouched)

`agent.ts` wiring (the `secretHit = findSecretPathInInput(input) ?? (Bash ?
findSecretPathInBashCommand(...) : undefined)` step-1 guard), `BASH_TOKEN_SEPARATOR`,
`SHELL_METACHARACTER_CLASS_BODY`, `SHELL_METACHARACTER_PATTERN`,
`SECRET_PATH_PATTERNS`, `matchSecretPath`, `findSecretPathInInput`,
`hasSecretNamedValueToken`, `isSafeReadOnlyGitCommand`, `DANGEROUS_BASH_PATTERNS`,
`matchSelfModificationPath` — all unchanged.

## TDD evidence

- **Red:** with the new tests added and BEFORE the implementation,
  `npx vitest run` → **6 failed / 419 passed**. The 3 genuinely-evading vectors
  failed at both levels: `findSecretPathInBashCommand` returned `undefined`
  (unit) and `canUseTool` returned `behavior:"allow"` instead of `"deny"`
  (routing) for `cat /tmp/x.p"e"m`, `cat /tmp/x.p''em`, `cat /tmp/x.pe\m`.
- **Green:** after adding `heuristicShellDequote` + the second pass,
  `npx vitest run` → **425 passed / 0 failed / 0 skipped** (7 files).
- **Refactor:** dequote isolated into its own documented helper; the scan loop
  iterates `[raw, dequoted]` sources — no duplication of the token loop.

## Verification (exact commands)

- Build: `cd apps/telegram-ceo && npm run build` (tsc) — clean.
- Tests: `cd apps/telegram-ceo && npx vitest run` — **425 passed, 0 failed, 0 skipped** (7 files).
- Latest `main` merged into the branch: `git merge origin/main` — "Already up to
  date" (main had not advanced since branch creation); build+tests re-confirmed
  green on the committed state. Branch pushed to origin.

## False-positive analysis

Legit list all auto-allow (verified by test): `npm run build`, `git status`,
`cat package.json`, `pytest -q`, `echo hi | grep h`,
`dotnet build ASPSBackend.sln -c Debug`, `node -e "1+1"`, `ls -la`,
`echo "hello.world"` (`.world` is not a secret pattern).

**Noted characteristic (pre-existing, NOT introduced here):** because the
tokenizer is coarse (splits on whitespace and never understands quoting), a
command whose free text legitimately contains a secret-suffix word — e.g.
`git commit -m "fix api.key handling"` — is hard-denied (token `api.key` →
`/\.key$/`). This already held under ASPS-780 (the raw pass splits the message
on whitespace and matches `api.key` directly); ASPS-782 does not widen it. Such
git-write commands are approval-gated anyway, and a hard-deny there is
fail-safe. Not fixed in this ticket (no scope creep); flagged for the security
gate's awareness.

## Spec docs to consider (do NOT edit here — CEO/Architect/TechWriter)

- `docs/architecture/decisions/ADR-005-ASPS-763-AGENT-TOOL-EXECUTION-PRIVILEGE-SEPARATION.md`
  — ASPS-780 added a note about the tool-level tokenized Bash secret scan. It
  may want a one-line update that the scan now also normalizes inner-quote /
  backslash-escape obfuscation of the secret suffix (ASPS-782). Flagged only —
  not edited.

## Continuation point

Work complete, committed (`234249e`), and pushed. No PR opened. Next: CEO runs
the three mandatory gates (QA + code review + security) on the branch, then
merges. Do NOT open the PR or merge before the gates pass.
