#!/usr/bin/env bash
# ASPS-742 — Phase 3: clone the ASPS repo and wire secrets for the Telegram
# CEO bot VPS.
#
# Runs AS aspsbot (or as root, which transparently re-execs as aspsbot via
# `runuser --login` so the clone / git config / secrets templates are all
# owned by aspsbot, not root). Idempotent — safe to re-run: an existing
# clone is fetched + fast-forwarded instead of re-cloned; existing secrets
# templates/files are never overwritten.
#
# Target: after 01-harden.sh + 02-toolchain.sh have run. Ubuntu 24.04 LTS.
#
# NOT YET EXECUTED: authored ahead of the VPS existing (Phase 0, ASPS-739 is
# still a user action). See deploy/vps/README.md.
#
# Secret placement (D2 / ASPS-745 item 1 — see README.md "Secret placement
# rule"): this script populates ${SECRETS_DIR} with TEMPLATES only
# (access_keys.env.example, telegram-ceo.env.example, and — for an HTTPS
# REPO_URL — a github-credentials placeholder). It never writes a real
# secret value; the operator copies the templates to their real filenames
# and fills them in. This is the belt to the agent's own path-guard
# suspenders (apps/telegram-ceo/src/security.ts's findSecretPathInInput /
# checkPathAllowed) — secrets simply never exist inside CLONE_PATH.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
SCRIPT_PATH="${SCRIPT_DIR}/$(basename -- "${BASH_SOURCE[0]}")"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

require_ubuntu_2404
load_config "$SCRIPT_DIR"

# REPO_URL/CLONE_PATH/GIT_USER_NAME/GIT_USER_EMAIL are only required by THIS
# script, not by 01/02 — validated here rather than in lib.sh's load_config
# so existing config.env files that predate Phase 3 don't suddenly fail
# 01-harden.sh/02-toolchain.sh over vars those scripts never use.
: "${REPO_URL:?REPO_URL must be set in config.env}"
: "${CLONE_PATH:?CLONE_PATH must be set in config.env}"
: "${GIT_USER_NAME:?GIT_USER_NAME must be set in config.env}"
: "${GIT_USER_EMAIL:?GIT_USER_EMAIL must be set in config.env}"

# From here on, everything must run AS aspsbot — re-exec if we're root.
require_user_or_reexec "$ASPSBOT_USER" "$SCRIPT_PATH"

bot_dir="${CLONE_PATH}/apps/telegram-ceo"
access_keys_template="${SECRETS_DIR}/access_keys.env.example"
bot_env_template="${SECRETS_DIR}/telegram-ceo.env.example"
github_credentials="${SECRETS_DIR}/github-credentials"

log_step "1/6 — clone or fast-forward ${REPO_URL} at ${CLONE_PATH}"
if [[ -d "${CLONE_PATH}/.git" ]]; then
    log_info "Existing clone found at ${CLONE_PATH} — fetching + fast-forwarding instead of re-cloning."
    git -C "$CLONE_PATH" remote set-url origin "$REPO_URL"
    git -C "$CLONE_PATH" fetch --prune origin

    default_branch="$(git -C "$CLONE_PATH" symbolic-ref --short refs/remotes/origin/HEAD 2>/dev/null | sed 's#^origin/##')"
    if [[ -z "$default_branch" ]]; then
        default_branch="main"
        log_warn "Could not determine origin/HEAD (older clone?) — assuming default branch '${default_branch}'."
    fi

    git -C "$CLONE_PATH" checkout "$default_branch"
    if ! git -C "$CLONE_PATH" merge --ff-only "origin/${default_branch}"; then
        log_error "Fast-forward merge of '${default_branch}' failed — local commits exist that diverge from origin. This script will not force-reset, rebase, or discard local work. Resolve manually (e.g. 'git -C ${CLONE_PATH} status'), then re-run."
        exit 1
    fi
    log_info "Updated ${CLONE_PATH} to latest origin/${default_branch} (fast-forward only)."
else
    log_info "No existing clone at ${CLONE_PATH} — cloning fresh."
    mkdir -p "$(dirname -- "$CLONE_PATH")"
    git clone "$REPO_URL" "$CLONE_PATH"
fi

log_step "2/6 — git identity + credential helper (headless push)"
git config --global user.name "$GIT_USER_NAME"
git config --global user.email "$GIT_USER_EMAIL"
git config --global init.defaultBranch main
git config --global --add safe.directory "$CLONE_PATH"

case "$REPO_URL" in
    git@*|ssh://*)
        log_info "REPO_URL uses SSH (${REPO_URL}) — git's credential-helper mechanism doesn't apply to SSH remotes. Headless push is expected to work via a deploy key under ~${ASPSBOT_USER}/.ssh (place it yourself; a private SSH key is a higher-sensitivity artifact than a PAT store file and is deliberately NOT generated or placed by this script). Skipping HTTPS credential-helper setup."
        ;;
    https://*)
        # Scoped to github.com only (credential."https://github.com".helper),
        # NOT a global default credential.helper — so this store is never
        # consulted for any other host. The token value is filled in by the
        # operator; this script only wires the mechanism + a placeholder.
        git config --global credential."https://github.com".helper "store --file=${github_credentials}"
        if [[ ! -f "$github_credentials" ]]; then
            ( umask 077 && cat > "$github_credentials" <<'EOF'
https://REPLACE_WITH_GITHUB_USERNAME:REPLACE_WITH_GITHUB_TOKEN@github.com
EOF
            )
            chmod 600 "$github_credentials"
            log_warn "Created placeholder ${github_credentials} (600). Fill in the real GitHub username + a fine-grained PAT (contents R/W scoped to isaacmendelson/ASPS only) before the agent's first push. Read by git's credential store helper, scoped to github.com (credential.\"https://github.com\".helper) — never a global default, and never inside ${CLONE_PATH}."
        else
            chmod 600 "$github_credentials" 2>/dev/null || true
            log_info "${github_credentials} already exists — left untouched (never overwriting an operator-filled secret)."
        fi
        ;;
    *)
        log_warn "Unrecognized REPO_URL scheme (${REPO_URL}) — skipping credential-helper setup. Configure git push auth manually."
        ;;
esac

log_step "3/6 — secrets templates in ${SECRETS_DIR} (ASPS-745 item 1 — outside the clone)"

if write_if_changed "$access_keys_template" <<'EOF'
# Template for ACCESS_KEYS.env on this VPS (ASPS-742 / ASPS-745 item 1).
#
# Copy this file to ACCESS_KEYS.env in this SAME directory (SECRETS_DIR,
# NOT the repo clone), fill in real values, confirm it is chmod 600, and
# NEVER commit it or move it into the repo clone.
#
# Mirrors the repo-root ACCESS_KEYS.env keys used by the CEO agent's own
# GitHub/JIRA workflows (see the project's reference_access_keys.md memory
# note). Loaded into the bot's systemd service as a second EnvironmentFile=
# (Phase 5, ASPS-744) so the agent's Bash-tool calls (git push, JIRA REST)
# see these as normal process environment variables — never read from a
# file inside the working tree the agent's own Read/Grep/Glob can reach.
GITHUB_REPO_URL=
GITHUB_USERNAME=
GITHUB_TOKEN=

JIRA_BASE_URL=
JIRA_EMAIL=
JIRA_API_TOKEN=
EOF
then
    chmod 600 "$access_keys_template"
    log_info "Wrote placeholder ${access_keys_template} (600)."
else
    chmod 600 "$access_keys_template" 2>/dev/null || true
    log_info "${access_keys_template} already up to date — skipped."
fi

if write_if_changed "$bot_env_template" <<EOF
# Template for the Telegram bot's runtime env on this VPS (ASPS-742).
#
# Copy this file to telegram-ceo.env in this SAME directory (SECRETS_DIR,
# NOT the repo clone), fill in the real Telegram/Claude values, confirm it
# is chmod 600, and NEVER commit it or move it into the repo clone. Loaded
# by telegram-ceo.service via EnvironmentFile= (Phase 5, ASPS-744). See
# apps/telegram-ceo/.env.example for the authoritative description of each
# variable and how the SDK's permission model uses them.
TELEGRAM_BOT_TOKEN=your-bot-token-here
CLAUDE_CODE_OAUTH_TOKEN=your-oauth-token-here
AUTHORIZED_USERS=123456789
WORKING_DIR=${CLONE_PATH}
MODEL=
MAX_TURNS=20
APPROVAL_TIMEOUT_MS=60000
EOF
then
    chmod 600 "$bot_env_template"
    log_info "Wrote placeholder ${bot_env_template} (600)."
else
    chmod 600 "$bot_env_template" 2>/dev/null || true
    log_info "${bot_env_template} already up to date — skipped."
fi

log_step "4/6 — build the bot (npm ci && npm run build)"
if [[ ! -d "$bot_dir" ]]; then
    log_error "${bot_dir} not found in the clone — did the clone/checkout succeed? Aborting before attempting a build."
    exit 1
fi
(
    cd "$bot_dir"
    if [[ -f package-lock.json ]]; then
        npm ci
    else
        log_warn "No package-lock.json found in ${bot_dir} — falling back to 'npm install' (less reproducible than 'npm ci')."
        npm install
    fi
    if ! npm run build; then
        log_error "npm run build FAILED in ${bot_dir} — see output above. Not proceeding to verify."
        exit 1
    fi
)
log_info "Bot built — ${bot_dir}/dist should now exist."

log_step "5/6 — verify"
verify_failed=false

log_info "git status (${CLONE_PATH}):"
git -C "$CLONE_PATH" status --short --branch || verify_failed=true

if [[ -f "${bot_dir}/dist/index.js" ]]; then
    log_info "Build artifact found: ${bot_dir}/dist/index.js"
else
    log_error "Build artifact MISSING: ${bot_dir}/dist/index.js"
    verify_failed=true
fi

for f in "$access_keys_template" "$bot_env_template"; do
    if [[ -f "$f" ]]; then
        perm="$(stat -c '%a' "$f" 2>/dev/null || echo '?')"
        if [[ "$perm" == "600" ]]; then
            log_info "Template present, mode 600: ${f}"
        else
            log_warn "Template present but mode is ${perm}, expected 600: ${f} — run 'chmod 600 ${f}'."
        fi
    else
        log_error "Expected template MISSING: ${f}"
        verify_failed=true
    fi
done

if [[ "$verify_failed" == true ]]; then
    log_error "One or more verification checks failed — see above."
    exit 1
fi

log_step "6/6 — next steps"
log_info "Nothing new is committed to the repo by this script (clone/fetch/checkout/merge --ff-only only)."
log_info "1. cp ${access_keys_template} ${SECRETS_DIR}/ACCESS_KEYS.env      — then fill in real GitHub/JIRA values (already 600 from cp)."
log_info "2. cp ${bot_env_template} ${SECRETS_DIR}/telegram-ceo.env         — then fill in real Telegram/Claude values (already 600 from cp)."
if [[ "$REPO_URL" == https://* ]]; then
    log_info "3. Fill in ${github_credentials} with the real GitHub username + fine-grained PAT."
fi
log_info "4. Run 05-service.sh (as root) to install and start telegram-ceo.service (Phase 5, ASPS-744)."
log_info "Note: this script only builds the bot (apps/telegram-ceo). A full 'dotnet build' of the ASPS backend on this box is a Phase 1/2 execution-gate concern, not part of Phase 3 — the backend itself stays on Azure (D1)."
