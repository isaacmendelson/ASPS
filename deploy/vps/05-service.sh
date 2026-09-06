#!/usr/bin/env bash
# ASPS-744 — Phase 5: install and start the 24/7 telegram-ceo systemd
# service.
#
# Renders deploy/vps/telegram-ceo.service (an @TOKEN@ template) using
# config.env, installs it to /etc/systemd/system/, enables it (survives
# reboot), and starts it. Refuses to start against placeholder secrets.
#
# Runs as ROOT (installing units + systemctl enable/daemon-reload require
# it). Idempotent — safe to re-run: the unit is only rewritten and
# daemon-reload only run if the rendered content actually changed
# (write-if-changed pattern, matching 01-harden.sh/02-toolchain.sh).
#
# Target: after 01-harden.sh, 02-toolchain.sh, AND 03-clone.sh have run, and
# the operator has filled in the two secrets files under SECRETS_DIR (see
# deploy/vps/README.md "Phase 5"). Ubuntu 24.04 LTS.
#
# NOT YET EXECUTED: authored ahead of the VPS existing (Phase 0, ASPS-739 is
# still a user action). See deploy/vps/README.md.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

require_root
require_ubuntu_2404
load_config "$SCRIPT_DIR"

: "${CLONE_PATH:?CLONE_PATH must be set in config.env}"

unit_name="telegram-ceo.service"
unit_template="${SCRIPT_DIR}/${unit_name}"
unit_target="/etc/systemd/system/${unit_name}"
bot_env_file="${SECRETS_DIR}/telegram-ceo.env"
access_keys_file="${SECRETS_DIR}/ACCESS_KEYS.env"

log_step "1/5 — pre-flight: secrets must be real, not placeholder templates"
guard_failed=false
if file_has_placeholder_value "$bot_env_file"; then
    log_error "${bot_env_file} is missing or still contains placeholder values (e.g. 'your-bot-token-here'). Copy ${SECRETS_DIR}/telegram-ceo.env.example to ${bot_env_file}, fill in real TELEGRAM_BOT_TOKEN / CLAUDE_CODE_OAUTH_TOKEN / AUTHORIZED_USERS, confirm chmod 600, then re-run."
    guard_failed=true
fi
if file_has_placeholder_value "$access_keys_file"; then
    log_error "${access_keys_file} is missing or still contains placeholder values. Copy ${SECRETS_DIR}/access_keys.env.example to ${access_keys_file}, fill in real GITHUB_TOKEN / JIRA_API_TOKEN, confirm chmod 600, then re-run."
    guard_failed=true
fi
if [[ "$guard_failed" == true ]]; then
    log_error "Refusing to install/start ${unit_name} against placeholder secrets. See deploy/vps/README.md 'Phase 5'."
    exit 1
fi
for f in "$bot_env_file" "$access_keys_file"; do
    perm="$(stat -c '%a' "$f" 2>/dev/null || echo '?')"
    if [[ "$perm" != "600" ]]; then
        log_warn "${f} has mode ${perm}, expected 600 — fixing."
        chmod 600 "$f"
    fi
done
log_info "Secrets present and don't match known placeholder markers (this checks for leftover placeholders, not that the real values are correct)."

if [[ ! -d "${CLONE_PATH}/apps/telegram-ceo/dist" ]]; then
    log_error "${CLONE_PATH}/apps/telegram-ceo/dist not found — the bot hasn't been built yet. Run 03-clone.sh first (it clones + npm ci + npm run build)."
    exit 1
fi

log_step "2/5 — render + install ${unit_name}"
if [[ ! -f "$unit_template" ]]; then
    log_error "Template ${unit_template} not found."
    exit 1
fi
rendered="$(mktemp)"
trap 'rm -f "$rendered"' EXIT

sed \
    -e "s#@ASPSBOT_USER@#${ASPSBOT_USER}#g" \
    -e "s#@CLONE_PATH@#${CLONE_PATH}#g" \
    -e "s#@SECRETS_DIR@#${SECRETS_DIR}#g" \
    "$unit_template" > "$rendered"

if grep -q '@[A-Z_]*@' "$rendered"; then
    log_error "Rendered unit still contains an unsubstituted @TOKEN@ — a placeholder was added to the template without a matching sed rule here. Aborting before install."
    grep -n '@[A-Z_]*@' "$rendered" || true
    exit 1
fi

unit_changed=false
if [[ -f "$unit_target" ]] && cmp -s "$rendered" "$unit_target"; then
    log_info "${unit_target} already up to date — skipped install."
else
    install -m 0644 "$rendered" "$unit_target"
    log_info "Installed ${unit_target}."
    unit_changed=true
fi

log_step "3/5 — daemon-reload (only if the unit changed)"
if [[ "$unit_changed" == true ]]; then
    systemctl daemon-reload
    log_info "systemd reloaded."
else
    log_info "No change — daemon-reload skipped."
fi

log_step "4/5 — enable + start"
systemctl enable "$unit_name" >/dev/null
systemctl restart "$unit_name"
sleep 2

log_step "5/5 — verify"
if systemctl is-active --quiet "$unit_name"; then
    log_info "${unit_name} is active."
else
    log_error "${unit_name} is NOT active. Recent logs:"
    journalctl -u "$unit_name" -n 50 --no-pager || true
    exit 1
fi
systemctl status "$unit_name" --no-pager -l || true

log_step "Done"
log_info "${unit_name} installed, enabled (survives reboot), and running."
log_info "Tail logs:      journalctl -u ${unit_name} -f"
log_info "Sanity-check no secrets leaked into the journal (expect no output):"
log_info "  journalctl -u ${unit_name} -n 200 --no-pager | grep -iE 'token|secret|api[_-]?key'"
log_info "If the service failed to start due to a permission error writing under \$HOME (e.g. the Claude Code CLI's own state directory), see deploy/vps/README.md 'Phase 5 — systemd hardening tradeoffs' for the ReadWritePaths fallback."
