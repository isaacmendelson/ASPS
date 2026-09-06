#!/usr/bin/env bash
# ASPS-741 — Phase 2 runtime toolchain for the Telegram CEO bot VPS.
#
# Installs: Node.js 20 LTS, git/ripgrep/build-essential, Claude Code CLI,
# .NET 8 SDK, system Python 3 (3.12 on Ubuntu 24.04) + venv/pip, Docker
# Engine + compose plugin.
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

log_step "2/7 — Node.js 20 LTS (NodeSource, keyring method)"
# ASPS-740/741 security remediation (Minor): the old `curl | bash -` setup
# script pipes an unauthenticated script straight into a root shell. Use
# NodeSource's documented keyring method instead — fetch the GPG key to
# /etc/apt/keyrings, reference it via signed-by= in a .list, same pattern
# as the Docker block below (DRY: one trusted-repo pattern for both).
if command -v node >/dev/null 2>&1 && node -v | grep -qE '^v20\.'; then
    log_info "Node 20 already installed ($(node -v)) — skipped."
else
    install -d -m 0755 /etc/apt/keyrings
    if [[ ! -f /etc/apt/keyrings/nodesource.gpg ]]; then
        curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
            | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg
        chmod a+r /etc/apt/keyrings/nodesource.gpg
    fi
    node_major=20
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_${node_major}.x nodistro main" \
        > /etc/apt/sources.list.d/nodesource.list
    apt-get update -y
    apt-get install -y nodejs
fi

log_step "3/7 — Claude Code CLI (@anthropic-ai/claude-code)"
# Needed for `claude setup-token` (Phase 0/3) and as the Agent SDK's runtime
# dependency for the Telegram bot (D2).
#
# Version: intentionally FLOATING, not pinned to a specific release
# (ASPS-740/741 security remediation Minor — documented per
# review-standards.md's "pin or document why floating is accepted").
# Rationale: Claude Code CLI ships frequent releases and is explicitly
# designed to be self-updating (`claude update`); pinning here would mean
# hand-editing this script on every upstream release just to stay current,
# with no compensating security benefit — this is a dev-tool CLI invoked
# interactively/by the bot, not a versioned artifact baked into a
# production image. This is a deliberate, narrow exception to CLAUDE.md's
# "pin base image versions" rule, which targets container base images for
# reproducible production builds.
#
# Idempotency guard: only install when the CLI is missing, so re-runs of
# this script don't force a reinstall/network round-trip every time.
if command -v claude >/dev/null 2>&1; then
    log_info "Claude Code CLI already installed ($(claude --version 2>/dev/null || echo 'version check failed')) — skipped. Run 'claude update' to refresh."
else
    npm install -g @anthropic-ai/claude-code
fi

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

log_step "5/7 — Python 3 (system, 3.12 on Ubuntu 24.04) + venv + pip"
# DECISION (ASPS-740/741 security remediation, Minor — deadsnakes dropped):
# the original script pulled Python 3.11 from the third-party deadsnakes
# PPA to match the desktop agent's stack (D3/Common desktop-agent Python
# version). Re-checked against what this VPS actually needs: per D4, the
# agent's build/test path for ASPS is `docker compose up` (Docker Engine,
# below), and the Analyzers carry their OWN Python inside their containers
# (per requirements.lock.txt — see feedback_docker_deps_from_lockfile.md).
# The VPS host itself never runs analyzer/desktop-agent Python directly —
# it only needs a general-purpose Python 3 for occasional host-level
# scripting/tooling. There is no concrete host-level requirement for
# exactly 3.11, so this installs Ubuntu 24.04's built-in python3 (3.12)
# instead of adding a third-party PPA to trust and keep patched. If a real
# host-level (non-container) need for 3.11 specifically turns up later,
# re-add deadsnakes here and document the concrete reason at that point.
apt-get install -y python3 python3-venv python3-dev python3-pip

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
report "python3" python3 --version
report "docker"  docker --version
report "rg"      rg --version

log_step "Done"
log_info "Toolchain installed. Log out/in as ${ASPSBOT_USER} (or reboot) before relying on the docker group membership."
log_info "Next: Phase 3 (clone repo & wire secrets) — separate script, later story (ASPS-742)."
