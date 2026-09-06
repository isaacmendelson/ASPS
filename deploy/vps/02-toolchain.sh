#!/usr/bin/env bash
# ASPS-741 — Phase 2 runtime toolchain for the Telegram CEO bot VPS.
#
# Installs: Node.js 20 LTS, git/ripgrep/build-essential, Claude Code CLI,
# .NET 8 SDK, Python 3.11 + venv/pip, Docker Engine + compose plugin.
#
# Target: Ubuntu 24.04 LTS, run as root, AFTER 01-harden.sh. Idempotent —
# safe to re-run.
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

export DEBIAN_FRONTEND=noninteractive

log_step "1/7 — base packages (git, ripgrep, build-essential, curl, ca-certificates)"
apt-get update -y
apt-get install -y --no-install-recommends \
    git ripgrep build-essential curl ca-certificates gnupg lsb-release apt-transport-https software-properties-common

log_step "2/7 — Node.js 20 LTS (NodeSource)"
if command -v node >/dev/null 2>&1 && node -v | grep -qE '^v20\.'; then
    log_info "Node 20 already installed ($(node -v)) — skipped."
else
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
    apt-get install -y nodejs
fi

log_step "3/7 — Claude Code CLI (@anthropic-ai/claude-code)"
# Needed for `claude setup-token` (Phase 0/3) and as the Agent SDK's runtime
# dependency for the Telegram bot (D2).
npm install -g @anthropic-ai/claude-code

log_step "4/7 — .NET 8 SDK (Microsoft apt feed)"
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -qE '^8\.'; then
    log_info ".NET 8 SDK already installed — skipped."
else
    if [[ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]]; then
        ms_deb="$(mktemp --suffix=.deb)"
        curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o "$ms_deb"
        dpkg -i "$ms_deb"
        rm -f "$ms_deb"
        apt-get update -y
    fi
    apt-get install -y dotnet-sdk-8.0
fi

log_step "5/7 — Python 3.11 + venv + pip (deadsnakes PPA — Ubuntu 24.04 ships 3.12 by default)"
if command -v python3.11 >/dev/null 2>&1; then
    log_info "python3.11 already installed — skipped."
else
    add-apt-repository -y ppa:deadsnakes/ppa
    apt-get update -y
    apt-get install -y python3.11 python3.11-venv python3.11-dev
fi
apt-get install -y python3-pip

log_step "6/7 — Docker Engine + compose plugin"
if command -v docker >/dev/null 2>&1; then
    log_info "Docker already installed ($(docker --version)) — skipped engine install."
else
    install -d -m 0755 /etc/apt/keyrings
    if [[ ! -f /etc/apt/keyrings/docker.asc ]]; then
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
        chmod a+r /etc/apt/keyrings/docker.asc
    fi
    arch="$(dpkg --print-architecture)"
    codename="$(awk -F= '/^VERSION_CODENAME=/{gsub(/"/,"",$2); print $2}' /etc/os-release)"
    echo "deb [arch=${arch} signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${codename} stable" \
        > /etc/apt/sources.list.d/docker.list
    apt-get update -y
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
systemctl enable --now docker >/dev/null 2>&1 || true

# --------------------------------------------------------------------------
# WARNING (ASPS-745 box-level item, docker-group privilege boundary):
# Membership in the `docker` group is EQUIVALENT TO ROOT — any member can
# bind-mount the host filesystem into a container and read/write anything
# root can. This is required for D4 (the CEO agent must be able to `docker
# compose up` the ASPS stack locally to verify its own work) but it means a
# compromised `aspsbot` account (e.g. a prompt-injection tricking the agent
# into running a malicious `docker run -v /:/host ...`) has a direct path to
# full host compromise, on top of whatever the agent's own Bash access
# already allows.
#
# This is KNOWN, ACCEPTED security debt for this phase, not introduced
# silently (per security-rules.md "known security debt" clause) — Security
# must explicitly review/accept or propose mitigation (e.g. rootless Docker,
# a dedicated non-aspsbot docker-group user the agent sudos into with
# extra friction, or dropping D4 entirely) as part of Phase 6 (ASPS-745).
# --------------------------------------------------------------------------
if id -nG "$ASPSBOT_USER" 2>/dev/null | grep -qw docker; then
    log_info "${ASPSBOT_USER} already in docker group — skipped."
else
    usermod -aG docker "$ASPSBOT_USER"
    log_warn "Added ${ASPSBOT_USER} to the docker group — this is root-equivalent. See the WARNING comment above this line in the script. ${ASPSBOT_USER} must log out/in for the new group to take effect."
fi

log_step "7/7 — verify (non-fatal; reports what's installed)"
report() {
    local label="$1"; shift
    if out="$("$@" 2>&1)"; then
        log_info "${label}: ${out}"
    else
        log_warn "${label}: NOT AVAILABLE / failed to run (${out:-no output})"
    fi
}
report "node"    node -v
report "npm"     npm -v
report "dotnet"  dotnet --version
report "python3.11" python3.11 --version
report "docker"  docker --version
report "rg"      rg --version

log_step "Done"
log_info "Toolchain installed. Log out/in as ${ASPSBOT_USER} (or reboot) before relying on the docker group membership."
log_info "Next: Phase 3 (clone repo & wire secrets) — separate script, later story (ASPS-742)."
