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
