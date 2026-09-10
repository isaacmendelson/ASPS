# `secrets_ref` Bundle Layout — ASPS-770

Status: locked (part of the ADR-006 project-config contract, decision 1 and decision 8).
Scope: layout, naming, and access invariants only. **No secret values appear
in this document or anywhere in this contract.**

## Principle

A `projects.json` record never carries a secret or a filesystem path to one
— it carries `secrets_ref`, an **opaque handle** (a slug — see
[`projects.schema.json`](projects.schema.json)'s `secrets_ref` pattern:
`^[a-z0-9][a-z0-9_-]{0,63}$`). This document defines how MASDP resolves that
handle to an on-host bundle. It generalizes the pattern already shipped for
the single-tenant Telegram CEO bot (`SECRETS_DIR=/home/aspsbot/secrets`, see
[`deploy/vps/README.md`](../../../deploy/vps/README.md) "Secret placement
rule" and [`deploy/vps/03-clone.sh`](../../../deploy/vps/03-clone.sh)) to N
projects.

## Where bundles live

- **`SECRETS_ROOT`** — a single directory on the MASDP host, **outside every
  project clone**, owned by the service account MASDP runs as (`aspsbot` on
  the current VPS), mode `700`. This is the direct generalization of
  today's `SECRETS_DIR`.
- Today (single project, SPS only): `SECRETS_DIR` itself *is* the one
  bundle — `SECRETS_ROOT == SECRETS_DIR`, one project, no subdirectory.
- Multi-tenant (this contract, once ASPS-763's sandbox lands and the bot is
  made multi-tenant per ADR-006 extraction-order step 6): `SECRETS_ROOT`
  holds **one subdirectory per `secrets_ref`**:

  ```
  ${SECRETS_ROOT}/
    <secrets_ref-A>/     e.g. masdp/
    <secrets_ref-B>/     e.g. sps/
    ...
  ```

- No clone (`working_dir` of any project) ever lives under `SECRETS_ROOT`,
  and `SECRETS_ROOT` never lives under any project's `working_dir`. The two
  trees are disjoint by construction — this is what makes the sandbox
  invariant below enforceable with a single deny rule instead of a
  per-project exclusion list.

## Per-project subdir naming convention

- The subdirectory name is **exactly** the project's `secrets_ref` value —
  no transformation, no prefix/suffix. `secrets_ref` is a slug specifically
  so it is a valid, unambiguous directory name on both POSIX and Windows
  hosts.
- Mode `700`, owned by the service account — defense in depth on top of
  `SECRETS_ROOT` itself already being `700` (belt + suspenders, matching
  the existing `SECRETS_DIR` convention).
- One project = one `secrets_ref` = one subdirectory. A `secrets_ref` value
  must be unique across the registry (same uniqueness expectation as
  `name`); reusing a `secrets_ref` across two project records would let one
  project's agent session load another project's credentials, which
  defeats the whole point of per-project bundles.

## What a bundle contains

Each `${SECRETS_ROOT}/<secrets_ref>/` directory holds **files, never
inline values in `projects.json` or any other checked-in doc**. Naming
generalizes the two files `03-clone.sh` already templates today
(`access_keys.env.example` / `telegram-ceo.env.example`) plus the
credential-store file `github-credentials`:

| File | Purpose | Generalizes |
|---|---|---|
| `access_keys.env` | This project's GitHub + JIRA credentials, as `KEY=value` lines consumed as an `EnvironmentFile=` / sourced env, never read from inside the clone. Keys: `GITHUB_REPO_URL`, `GITHUB_USERNAME`, `GITHUB_TOKEN`, `JIRA_BASE_URL`, `JIRA_EMAIL`, `JIRA_API_TOKEN`. | Today's single `ACCESS_KEYS.env` / `access_keys.env.example` (see `deploy/vps/03-clone.sh` step 3/6). |
| `github-credentials` | Git credential-store line for HTTPS push (`https://<user>:<token>@github.com`), wired via a **per-host, per-remote-scoped** `git config credential."https://github.com".helper` — see the naming caveat below for why this one file is host-scoped, not literally per-project on today's git. | Today's `${SECRETS_DIR}/github-credentials` (`03-clone.sh` step 2/6). |
| `agent.env` (optional) | Project-specific runtime secrets the *project's own* services need, beyond GitHub/JIRA — e.g. SPS's `CQRS_SHARED_SECRET`, Keycloak client secret, CURVE key material references. Out of scope for ASPS-770 to migrate (those still live in SPS's own `appsettings.*`/`.env` today); this slot is reserved so a later story has a defined place to put them without re-opening this contract. | New — no direct predecessor; reserves the slot ADR-006's "each project carries its own what" principle implies. |

No other file names are reserved by this contract version; a project may
need none of these (e.g. a read-only/no-push project could omit
`github-credentials`), in which case the corresponding file is simply
absent — not an empty placeholder.

### Naming caveat — git credential-store scoping is per-host, not per-bundle, today

`git config credential."https://github.com".helper` is scoped by **URL
host**, not by an arbitrary key — one host (`github.com`) resolves to one
configured helper file for the whole OS user (`aspsbot`) running MASDP,
regardless of which project's working tree the `git push` runs from. So
today's mechanism, unmodified, cannot hold two *different* GitHub
credentials for two different projects (`secrets_ref=masdp` vs.
`secrets_ref=sps`) simultaneously if both remotes are `github.com` — this
is a real gap the multi-tenant bot work (ADR-006 extraction-order step 6)
must resolve, not something this contract can wave away. Two known options,
left open for that story rather than decided here (out of scope for
ASPS-770 — freezing the *registry/bundle shape*, not the runtime credential
switch mechanism):

1. Switch the credential helper invocation per-session to point at the
   active project's `github-credentials` file (`git -c
   credential.https://github.com.helper="store --file=<bundle>/github-credentials" push`),
   rather than a single global `git config`.
2. Use per-project fine-grained PATs passed directly on the remote URL or
   via `GIT_ASKPASS`, sidestepping the global credential-store mechanism
   entirely.

This document still reserves `github-credentials` as the per-bundle file
name either way — only the *wiring* mechanism into git is undecided.

## The sandbox-deny invariant (ties to ASPS-763 / ADR-005)

**A sandboxed exec for any project must not be able to read `SECRETS_ROOT`
at all** — not its own bundle, and not any other project's:

- The bot's per-exec sandbox (bubblewrap, `deploy/vps/06-sandbox.sh`, ADR-005)
  bind-mounts the active project's `working_dir` read-write and the rest of
  the filesystem read-only or masked. `SECRETS_ROOT` must be **outside**
  every such bind-mount's writable scope and, per the ASPS-763 sandbox
  design, denied entirely (not merely read-only) to the sandboxed process
  tree — secrets reach the agent's process environment via
  `EnvironmentFile=`/equivalent injection at the **supervisor** level
  (systemd today), before the sandbox is entered, never via a filesystem
  read the sandboxed command itself performs. This is the same principle
  already documented for the single-project case in
  `deploy/vps/README.md`'s "Secret placement rule" (M5 in the Phase 6
  audit) — this contract extends it from "one bundle, always denied" to "N
  bundles, always denied, including the caller's own."
- Defense in depth, independent of the sandbox: the existing path-guard in
  `apps/telegram-ceo/src/security.ts` (`findSecretPathInInput` /
  `checkPathAllowed`) already hard-denies `*.env`/`*.key`/`ACCESS_KEYS*`
  patterns anywhere in tool input, including inside a working tree. That
  guard's deny-list should be reviewed against this contract's concrete
  file names (`access_keys.env`, `github-credentials`, `agent.env`) when the
  multi-tenant bot work lands, but is not modified by ASPS-770 itself
  (application code, not this story's scope).
- **No cross-project read**, specifically: a session bound to project X's
  `working_dir` must never be able to path-traverse into
  `${SECRETS_ROOT}/<Y's secrets_ref>/` even if X's own bundle were
  (incorrectly) made readable — the deny rule targets `SECRETS_ROOT` as a
  whole, not a per-project allow-list, so this invariant holds by
  construction rather than by remembering to update an exclusion list every
  time a project is added.

## What this document does NOT do

- It does not create `SECRETS_ROOT` or any bundle on any host — that is
  provisioning-script work (`deploy/vps/*.sh`), out of scope for this
  design-only story.
- It does not contain, reference by path, or hint at any real secret value
  — every path/name above is either a placeholder slug (`<secrets_ref>`) or
  a real *file name convention*, never a real credential.
- It does not decide the git credential-store multi-tenant wiring (see the
  naming caveat above) — flagged for the story that makes the bot
  multi-tenant (ADR-006 extraction-order step 6).
