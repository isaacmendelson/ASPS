#!/usr/bin/env bash
# Shared helpers for deploy/vps/*.sh (ASPS-740 / ASPS-741).
# Sourced, not executed directly. Keeps logging/config-loading/idempotency
# helpers in one place (DRY) instead of duplicated across 01-harden.sh and
# 02-toolchain.sh.

# --- logging -----------------------------------------------------------

log_info()  { printf '\033[1;34m[INFO]\033[0m  %s\n' "$*"; }
log_warn()  { printf '\033[1;33m[WARN]\033[0m  %s\n' "$*"; }
log_error() { printf '\033[1;31m[ERROR]\033[0m %s\n' "$*" >&2; }
log_step()  { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# --- preconditions -------------------------------------------------------

require_root() {
    if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
        log_error "This script must run as root (first-boot provisioning). Re-run with sudo/root."
        exit 1
    fi
}

require_ubuntu_2404() {
    if [[ -r /etc/os-release ]]; then
        # shellcheck source=/dev/null
        . /etc/os-release
        if [[ "${ID:-}" != "ubuntu" || "${VERSION_ID:-}" != "24.04" ]]; then
            log_warn "Expected Ubuntu 24.04 LTS, detected ${PRETTY_NAME:-unknown}. Continuing anyway — verify manually."
        fi
    else
        log_warn "/etc/os-release not found — cannot confirm this is Ubuntu 24.04. Continuing anyway."
    fi
}

# --- config loading -------------------------------------------------------

# Loads deploy/vps/config.env (next to the calling script) and validates the
# required variables are non-empty / non-placeholder. Exits with a clear
# message if config.env is missing (never silently falls back to
# config.env.example — that file only contains placeholder values).
load_config() {
    local script_dir="$1"
    local config_file="${script_dir}/config.env"
    local example_file="${script_dir}/config.env.example"

    if [[ ! -f "$config_file" ]]; then
        log_error "Missing ${config_file}."
        log_error "Copy ${example_file} to config.env, fill in real values, then re-run."
        exit 1
    fi

    # shellcheck source=/dev/null
    source "$config_file"

    : "${ASPSBOT_USER:?ASPSBOT_USER must be set in config.env}"
    : "${ASPSBOT_SSH_PUBLIC_KEY:?ASPSBOT_SSH_PUBLIC_KEY must be set in config.env}"
    : "${SSH_PORT:?SSH_PORT must be set in config.env}"
    : "${TIMEZONE:?TIMEZONE must be set in config.env}"
    : "${HOSTNAME_FQDN:?HOSTNAME_FQDN must be set in config.env}"
    : "${SWAP_SIZE_GB:?SWAP_SIZE_GB must be set in config.env}"
    : "${SECRETS_DIR:?SECRETS_DIR must be set in config.env}"

    # LOCK_ROOT is optional (defaults to false — see 01-harden.sh step 4 /
    # ASPS-740 security remediation). Not required so existing config.env
    # files from before this option existed keep working unchanged.
    : "${LOCK_ROOT:=false}"
    case "${LOCK_ROOT,,}" in
        true|false) ;;
        *)
            log_error "LOCK_ROOT must be 'true' or 'false' in config.env, got: ${LOCK_ROOT}"
            exit 1
            ;;
    esac

    if [[ "$ASPSBOT_SSH_PUBLIC_KEY" == *"your-key-here"* ]]; then
        log_error "ASPSBOT_SSH_PUBLIC_KEY in config.env is still the placeholder value."
        log_error "Paste your real SSH public key (e.g. contents of ~/.ssh/id_ed25519.pub) and re-run."
        exit 1
    fi

    case "$SSH_PORT" in
        ''|*[!0-9]*)
            log_error "SSH_PORT must be numeric, got: ${SSH_PORT}"
            exit 1
            ;;
    esac
}

# --- idempotency helpers -------------------------------------------------

# Writes $2 (content, via stdin heredoc from the caller) to $1 only if the
# target doesn't already have identical content. Avoids unnecessary
# rewrites/reloads on re-run and makes "did this change?" easy to log.
# Usage: write_if_changed /path/to/file <<'EOF' ... EOF
write_if_changed() {
    local target="$1"
    local tmp
    tmp="$(mktemp)"
    cat > "$tmp"
    if [[ -f "$target" ]] && cmp -s "$tmp" "$target"; then
        rm -f "$tmp"
        return 1 # unchanged
    fi
    mkdir -p "$(dirname "$target")"
    mv "$tmp" "$target"
    return 0 # changed
}

package_installed() {
    dpkg -s "$1" >/dev/null 2>&1
}

# --- sshd verification (ASPS-740 security remediation) -------------------
#
# Both helpers below are the AUTHORITATIVE post-merge checks — they must be
# used as the actual gate before opening UFW / declaring success, not just
# as extra logging. File contents/naming alone (drop-in order, sshd -t
# syntax validation) are NOT sufficient: Ubuntu 24.04 socket-activates sshd
# (sshd_config's "Port" is silently ignored while ssh.socket owns the
# listening socket) and sshd_config.d/*.conf is first-value-wins in lexical
# order (an earlier drop-in, e.g. a cloud image's 50-cloud-init.conf, can
# silently win over ours) — in both cases `sshd -t` still reports success.

# Returns 0 only if `sshd -T`'s fully-merged, effective configuration
# actually has PasswordAuthentication no / PermitRootLogin no /
# PubkeyAuthentication yes. This reflects every sshd_config.d/*.conf
# merged together (first-value-wins), so it catches an earlier drop-in
# (e.g. 50-cloud-init.conf) winning over ours regardless of filename.
assert_effective_sshd_config() {
    local merged pa pr pk
    merged="$(sshd -T 2>/dev/null)" || true
    if [[ -z "$merged" ]]; then
        log_error "sshd -T produced no output — cannot verify the effective sshd configuration."
        return 1
    fi
    pa="$(awk 'tolower($1)=="passwordauthentication"{print tolower($2)}' <<<"$merged")"
    pr="$(awk 'tolower($1)=="permitrootlogin"{print tolower($2)}' <<<"$merged")"
    pk="$(awk 'tolower($1)=="pubkeyauthentication"{print tolower($2)}' <<<"$merged")"
    if [[ "$pa" != "no" || "$pr" != "no" || "$pk" != "yes" ]]; then
        log_error "Effective sshd config (sshd -T, the authoritative post-merge view) does not match the hardening intent: PasswordAuthentication=${pa:-<unset>} PermitRootLogin=${pr:-<unset>} PubkeyAuthentication=${pk:-<unset>}. A lower-sorting or vendor drop-in (e.g. 50-cloud-init.conf) may still be winning over our hardening drop-in. Aborting."
        return 1
    fi
    log_info "Effective sshd config confirmed (sshd -T): PasswordAuthentication=no PermitRootLogin=no PubkeyAuthentication=yes."
    return 0
}

# Returns 0 only if sshd is ACTUALLY listening on the given port right now
# (ss -tlnp), not merely configured to. Catches Ubuntu 24.04's ssh.socket
# socket-activation silently keeping sshd on :22 (or wherever ssh.socket
# points it) while sshd_config's "Port" directive — which sshd -t validates
# as syntactically fine — is ignored.
assert_sshd_listening() {
    local port="$1"
    local matches
    matches="$(ss -tlnp 2>/dev/null | awk -v p=":${port}\$" '$4 ~ p')" || true
    if [[ -z "$matches" ]]; then
        log_error "No listener found on port ${port} (ss -tlnp). sshd is not actually bound there — aborting BEFORE touching UFW so the box is never left reachable only on a port UFW would deny."
        return 1
    fi
    if ! grep -q 'sshd' <<<"$matches"; then
        log_error "Port ${port} has a listener but it is not sshd (ss -tlnp): ${matches}. Aborting before touching UFW."
        return 1
    fi
    log_info "Confirmed sshd is listening on ${port}: ${matches}"
    return 0
}

# Returns 0 if ssh.socket (systemd socket activation) currently owns the
# SSH listening socket — the Ubuntu 24.04 default. When true, sshd_config's
# "Port" directive has no effect until socket activation is disabled and
# ssh.service takes over binding the port itself.
ssh_socket_activation_active() {
    systemctl is-enabled ssh.socket >/dev/null 2>&1 || systemctl is-active ssh.socket >/dev/null 2>&1
}
