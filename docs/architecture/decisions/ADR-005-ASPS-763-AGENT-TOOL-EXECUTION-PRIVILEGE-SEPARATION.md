# ADR-005 — ASPS-763 Privilege-Separate the Telegram CEO Bot's Agent Tool Execution

- Status: Proposed
- Date: 2026-09-08
- Jira: ASPS-763 — Privilege-separate the Telegram CEO bot's agent tool execution
- Decision owners: Architect
- Depends on: ASPS-743 (deny-by-default canUseTool, settingSources:[] isolation),
  ASPS-748 (bot-scoped read-only MCP servers), ASPS-745 (VPS systemd hardening)
- Related: ASPS-762 (approval relaxation — blocked by this ADR)

## Context

The ASPS Telegram CEO bot (`apps/telegram-ceo`, Node + `@anthropic-ai/claude-agent-sdk`)
runs on a Hostinger VPS as OS user `aspsbot`. The SDK's `query()` spawns the Bash
tool and anything it launches as children of the bot process — i.e. as `aspsbot`,
which can read `/home/aspsbot/secrets/` (GitHub PAT, JIRA creds, Telegram + Claude
tokens) and holds a stored git-push credential (`credential.helper=store --file=
/home/aspsbot/secrets/github-credentials`).

ASPS-762 sought to auto-allow routine dev work — including running tests
(`npm test`/`jest`/`pytest`) — without a Telegram approval. Two security gates
FAILED it (Blocker): running tests is arbitrary code execution as `aspsbot`. An
injected agent can `Write evil.test.js` (in-repo, auto-allowed) then `jest` it
(auto-allowed), and that code can `fs.readFileSync('/home/aspsbot/secrets/
ACCESS_KEYS.env')` to exfiltrate write-scoped tokens, or `git push HEAD:main` via
the ambient credential — bypassing the git-push approval gate and the tool
path-guard (which only constrains the Edit/Read tools, not code they spawn). An
earlier fix scrubbed secrets from the tool child's environment via the SDK
`Options.env`; that closed only the env-var door, not `fs.readFileSync` of the
secret file, nor the ambient credential-helper.

Investigation of the SDK surface (`sdk.d.ts`) established:
- There is no per-Bash-exec uid hook and no shell-wrapper hook. `spawnClaudeCodeProcess`
  is whole-CLI granularity; `toolAliases` can only redirect Bash to an MCP tool.
- The SDK ships a first-class `sandbox` option (Linux = bubblewrap) with
  `filesystem.denyRead/allowWrite`, `credentials.files/envVars` (mode deny/mask),
  and `network` controls — a per-exec namespace jail whose mount namespace is
  inherited by any child the command spawns.
- `createSdkMcpServer`/`tool()` define custom tools that execute in the SDK host
  (the bot's own Node process), outside any command sandbox, with access to the
  bot-process credentials.

## Decision

Adopt a two-part privilege separation:

1. **Contain every Bash execution in the SDK's built-in bubblewrap sandbox**
   (`sandbox.enabled:true`, `failIfUnavailable:true`):
   - `filesystem.denyRead` the secrets directory; `allowWrite` limited to the repo
     clone; rest of FS read-only.
   - `credentials.files`/`credentials.envVars` deny the github-credentials file and
     all tokens, so the sandboxed command and its children cannot see them.
   - Do NOT set `autoAllowBashIfSandboxed`. `canUseTool` remains the sole authority:
     it auto-allows Bash (after the destructive-pattern hard-deny) because the
     sandbox now bounds the blast radius.

2. **Move privileged operations into an in-process gated MCP server**
   (`createSdkMcpServer({name:'ceo-privileged'})`), whose handlers run in the bot
   process (as `aspsbot`, unsandboxed, holding the creds):
   - `git_push` (approved push), `jira_transition`/`jira_comment`/`jira_update_issue`,
     `github_create_pr`/`github_comment`.
   - This server gets NO auto-allow wildcard and NO `allowedTools` entry, so every
     call falls through to `canUseTool`'s deny-by-default branch → Telegram approval.

The read-only bot-scoped MCP servers (ASPS-748) and their auto-allow wildcards are
unchanged. `settingSources:[]` isolation is unchanged.

VPS provisioning changes: install `bubblewrap`; relax the systemd unit's
`RestrictNamespaces=yes` to permit the user+mount namespaces bwrap needs; ensure
Ubuntu 24.04's unprivileged-userns AppArmor restriction
(`kernel.apparmor_restrict_unprivileged_userns`) permits bwrap (package profile or
sysctl); point `sandbox.bwrapPath` at the installed binary. Perform these with the
Hostinger console fallback available (per the ASPS-745 SSH-lockout lesson).

## Consequences

Positive:
- The exact ASPS-762 Blocker exploit is closed: sandboxed test/build code cannot
  read secrets or use the ambient push credential.
- Unblocks ASPS-762: sandboxed Bash (build, test, run) becomes auto-allowed with a
  bounded blast radius.
- Write capability (JIRA/GitHub) is restored but per-call Telegram-gated; approved
  git push still works through the gated in-process tool.
- No second OS user, no second systemd unit, no local socket — the broker collapses
  into the bot via in-process MCP.
- `canUseTool` stays the single policy choke point (ASPS-743 invariant preserved).

Negative / residual:
- Sandboxed Bash retains network egress (needed for npm/pip/dotnet/git fetch); an
  injected agent could exfiltrate repo *source* (already on GitHub) — secrets and
  creds are unreachable. Optional follow-up: `sandbox.network` domain allowlist.
- Relaxing `RestrictNamespaces=` slightly widens the systemd sandbox; the containment
  moves to bwrap. Net security is strongly positive.
- bubblewrap on Ubuntu 24.04 needs userns enabled — a box change requiring care
  (console fallback), coupled to the deferred `SystemCallFilter=@system-service`
  work (must not block clone/unshare).
- The `ceo-privileged` MCP handlers are new trusted code holding creds — must be
  reviewed as a security boundary (input validation on refspec/issue-key/PR body).

## Alternatives considered

- **A. Dedicated OS user (`aspsagent`) for tool exec.** SDK has no per-Bash uid hook;
  would require running the whole CLI as `aspsagent` + a broker for Telegram/secrets,
  re-homing `~/.claude`/`~/.gitconfig`, and reworking docker-group access for the
  mcp-atlassian stdio child. High cost, most moving parts. Rejected.
- **B. Hand-rolled nsjail/firejail wrapper via `toolAliases:{Bash:'mcp__x__bash'}`.**
  Re-implements the built-in sandbox with less coverage (no credential masking / network
  layer). Rejected as redundant.
- **C. Container for the whole runner.** Heaviest; repo bind-mount + docker-in-the-loop;
  leans on the docker-group root-equiv debt. Rejected.
- **D. Separate broker daemon + user + local socket (classic split-process).** Correct
  but most surface. Adopted in *shape* (privileged ops brokered) but realized in-process
  via `createSdkMcpServer`, removing the daemon/user/socket.
- **E. `spawnClaudeCodeProcess` to launch the CLI under bwrap/another user.** Whole-CLI
  granularity breaks the docker MCP stdio child and `~/.claude` state; the per-exec
  `sandbox` option is the right granularity. Rejected.

## Implementation plan (gated stories)

Each story is mergeable through QA + code review + security gate. Sequencing:
1 → 2 must land before 5; 3 and 4 precede 5 so the write path exists before Bash push
is deprecated in the prompt. Story 5 is the only one that relaxes an approval and is
gated behind on-box verification of 1–4.

1. **ASPS-763-1 VPS bubblewrap enablement** — install `bubblewrap`; relax
   `RestrictNamespaces=`; resolve Ubuntu 24.04 userns AppArmor; smoke-test as `aspsbot`.
   No bot behavior change. Apply with Hostinger console open; rollback restores the unit.
2. **ASPS-763-2 Enable the sandbox in `buildOptions`** — `sandbox:{enabled,failIfUnavailable,
   bwrapPath,filesystem.denyRead/allowWrite,credentials.files/envVars}`. Bash still gated.
   Negative tests: secret read fails in-sandbox. Requires story 1 on the box first.
3. **ASPS-763-3 In-process `ceo-privileged` MCP (JIRA/GitHub writes)** — `createSdkMcpServer`
   with jira/github write tools, no wildcard → per-call Telegram approval. Restores write
   capability behind approval.
4. **ASPS-763-4 `git_push` privileged tool + block ambient push** — approved push via the
   in-process tool; verify sandboxed Bash push has no credential. Force-push still hard-denied.
5. **ASPS-763-5 Auto-allow sandboxed Bash (the ASPS-762 relaxation)** — after the
   destructive-pattern hard-deny, auto-allow Bash now that 2–4 make it safe. Merge only
   after 1–4 verified on the box. Closes ASPS-762.
6. **ASPS-763-6 (optional) egress allowlist** — `sandbox.network` allowlist to narrow the
   repo-source-exfil residual.

## What remains gated vs newly auto-allowed

- **Newly auto-allowed:** sandboxed Bash for dev (build, test, run programs) — contained.
- **Still gated (per-call Telegram approval):** `git push` (via `git_push` tool), JIRA
  writes, GitHub PR writes — all via the `ceo-privileged` in-process server.
- **Still hard-denied (never approvable):** `rm -rf`, `git push --force`,
  `git reset --hard`, etc.
- **Still blocked entirely:** reading `/home/aspsbot/secrets/`; ambient git push.
- **Accepted residual:** sandboxed Bash egress can exfiltrate repo source (already public);
  secrets/creds unreachable. Story 6 narrows it.

## Box changes requiring care (DevOps / Security)

1. `RestrictNamespaces=yes` → relaxed in the systemd unit (bwrap needs user+mount ns).
2. Ubuntu 24.04 `kernel.apparmor_restrict_unprivileged_userns=1` can block bwrap — resolve
   via the package AppArmor profile or a sysctl drop-in; verify with an on-box smoke test.
3. Coupling with the deferred `SystemCallFilter=@system-service` — if later enabled it MUST
   allow `clone`/`unshare`/`mount` for bwrap.
4. Apply with the Hostinger browser-console fallback open (`LOCK_ROOT=false`), per the
   recovered SSH-lockout incident (HARDENING §4).
