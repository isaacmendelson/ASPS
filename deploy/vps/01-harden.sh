#!/usr/bin/env bash
# ASPS-740 — Phase 1 baseline hardening for the Telegram CEO bot VPS.
#
# Target: fresh Ubuntu 24.04 LTS, run as root over the initial provider
# console/root SSH session (Hostinger). Idempotent — safe to re-run.
#
# NOT YET EXECUTED: authored ahead of the VPS existing (Phase 0, ASPS-739 is
# still a user action). Do not run this against a real box until Phase 0 is
# done and you have reviewed config.env. See deploy/vps/README.md.
#
# IMPORTANT — do not lock yourself out:
#   Keep your CURRENT root/console session open until you have confirmed, in
#   a SEPARATE terminal, that you can log in as `${ASPSBOT_USER}` on the
#   (possibly new) SSH port with your key. This script reloads sshd (not a
#   full restart) after validating the new config with `sshd -t`, but a
#   misconfigured cloud firewall/security group in front of the box, or a
#   wrong/missing public key, can still lock you out. Test before closing.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

require_root
require_ubuntu_2404
load_config "$SCRIPT_DIR"

log_step "1/9 — apt update && full-upgrade"
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get full-upgrade -y

log_step "2/9 — unattended-upgrades"
if ! package_installed unattended-upgrades; then
    apt-get install -y unattended-upgrades apt-listchanges
fi
if write_if_changed /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF
then
    log_info "20auto-upgrades written"
else
    log_info "20auto-upgrades already correct"
fi
systemctl enable --now unattended-upgrades.service >/dev/null 2>&1 || true

log_step "3/9 — non-root sudo user: ${ASPSBOT_USER}"
if ! id -u "$ASPSBOT_USER" >/dev/null 2>&1; then
    useradd --create-home --shell /bin/bash --groups sudo "$ASPSBOT_USER"
    log_info "Created user ${ASPSBOT_USER} (sudo group)."
else
    usermod -aG sudo "$ASPSBOT_USER"
    log_info "User ${ASPSBOT_USER} already exists — ensured sudo membership."
fi

user_home="/home/${ASPSBOT_USER}"
ssh_dir="${user_home}/.ssh"
authorized_keys="${ssh_dir}/authorized_keys"

install -d -m 700 -o "$ASPSBOT_USER" -g "$ASPSBOT_USER" "$ssh_dir"
touch "$authorized_keys"
chmod 600 "$authorized_keys"
chown "$ASPSBOT_USER":"$ASPSBOT_USER" "$authorized_keys"

if ! grep -qxF "$ASPSBOT_SSH_PUBLIC_KEY" "$authorized_keys" 2>/dev/null; then
    echo "$ASPSBOT_SSH_PUBLIC_KEY" >> "$authorized_keys"
    log_info "Installed SSH public key into ${authorized_keys}."
else
    log_info "SSH public key already present in ${authorized_keys} — skipped."
fi

# Passwordless sudo is intentionally NOT configured — aspsbot has an
# interactive password for `sudo` prompts (set manually by the operator on
# first login, e.g. `passwd aspsbot` from the console). SSH auth to the
# account itself is key-only (enforced below via sshd_config).

log_step "4/9 — lock direct root login"
# Belt-and-suspenders: sshd_config's PermitRootLogin no (below) is the real
# control. Locking the root password additionally prevents root login via
# any other console/recovery path that might fall back to password auth.
# Skipped harmlessly if root has no usable password already.
if passwd -S root 2>/dev/null | awk '{print $2}' | grep -qx 'L'; then
    log_info "root password already locked — skipped."
else
    passwd -l root
    log_info "root password locked (passwd -l root)."
fi

log_step "5/9 — sshd hardening (drop-in, validated before reload)"
sshd_dropin="/etc/ssh/sshd_config.d/99-aspsbot-hardening.conf"
if write_if_changed "$sshd_dropin" <<EOF
# Managed by deploy/vps/01-harden.sh (ASPS-740). Do not hand-edit — re-run
# the script to change these values via config.env instead.
Port ${SSH_PORT}
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
AllowUsers ${ASPSBOT_USER}
X11Forwarding no
AllowAgentForwarding no
AllowTcpForwarding no
ClientAliveInterval 300
ClientAliveCountMax 2
MaxAuthTries 3
EOF
then
    log_info "Wrote ${sshd_dropin}."
    log_info "Validating sshd config (sshd -t)..."
    if ! sshd -t; then
        log_error "sshd -t FAILED against the new config. Reverting drop-in and aborting."
        rm -f "$sshd_dropin"
        exit 1
    fi
    log_info "sshd -t OK. Reloading sshd (not restarting) to apply."
    log_warn "KEEP THIS SESSION OPEN. Test a new login now: ssh -p ${SSH_PORT} ${ASPSBOT_USER}@<vps-ip>"
    systemctl reload ssh 2>/dev/null || systemctl reload sshd 2>/dev/null || {
        log_error "Could not reload ssh/sshd via systemctl — reload manually and verify before disconnecting."
        exit 1
    }
else
    log_info "${sshd_dropin} already correct — skipped reload."
fi

log_step "6/9 — UFW firewall (default deny incoming, allow outgoing, SSH only)"
if ! package_installed ufw; then
    apt-get install -y ufw
fi
ufw default deny incoming >/dev/null
ufw default allow outgoing >/dev/null
if ! ufw status | grep -qE "^${SSH_PORT}/tcp\b"; then
    ufw allow "${SSH_PORT}/tcp" comment 'SSH (aspsbot)'
    log_info "UFW: allowed ${SSH_PORT}/tcp."
else
    log_info "UFW: ${SSH_PORT}/tcp already allowed — skipped."
fi
# Bot uses OUTBOUND long-poll only (Telegram/Anthropic/GitHub/JIRA) — no
# inbound app port is opened here. Egress restriction to those specific
# endpoints (ASPS-745 box-level item 2) is deferred to Phase 6 (security
# deepening) — deliberately NOT implemented in this baseline script; see
# README.md "Deferred to later phases".
ufw --force enable >/dev/null
log_info "UFW enabled. Status:"
ufw status verbose

log_step "7/9 — fail2ban (sshd jail)"
if ! package_installed fail2ban; then
    apt-get install -y fail2ban
fi
if write_if_changed /etc/fail2ban/jail.local <<EOF
[DEFAULT]
bantime  = 1h
findtime = 10m
maxretry = 5
backend  = systemd

[sshd]
enabled = true
port    = ${SSH_PORT}
EOF
then
    log_info "jail.local written"
else
    log_info "jail.local already correct"
fi
systemctl enable --now fail2ban >/dev/null 2>&1 || true
systemctl restart fail2ban
log_info "fail2ban active: $(fail2ban-client status sshd 2>/dev/null | tr '\n' ' ' || echo 'status unavailable')"

log_step "8/9 — swap (${SWAP_SIZE_GB}G), timezone, hostname"
swapfile="/swapfile"
if swapon --show=NAME --noheadings 2>/dev/null | grep -qx "$swapfile" || grep -qE "^${swapfile}\s" /etc/fstab 2>/dev/null; then
    log_info "Swap file already configured — skipped."
else
    fallocate -l "${SWAP_SIZE_GB}G" "$swapfile" || dd if=/dev/zero of="$swapfile" bs=1M count=$((SWAP_SIZE_GB * 1024))
    chmod 600 "$swapfile"
    mkswap "$swapfile"
    swapon "$swapfile"
    echo "${swapfile} none swap sw 0 0" >> /etc/fstab
    log_info "Created and enabled ${SWAP_SIZE_GB}G swap at ${swapfile}."
fi
if ! grep -qx 'vm.swappiness=10' /etc/sysctl.d/99-aspsbot-swap.conf 2>/dev/null; then
    echo 'vm.swappiness=10' > /etc/sysctl.d/99-aspsbot-swap.conf
    sysctl -w vm.swappiness=10 >/dev/null
fi

current_tz="$(timedatectl show -p Timezone --value 2>/dev/null || echo '')"
if [[ "$current_tz" != "$TIMEZONE" ]]; then
    timedatectl set-timezone "$TIMEZONE"
    log_info "Timezone set to ${TIMEZONE}."
else
    log_info "Timezone already ${TIMEZONE} — skipped."
fi

current_hostname="$(hostnamectl --static 2>/dev/null || echo '')"
if [[ "$current_hostname" != "$HOSTNAME_FQDN" ]]; then
    hostnamectl set-hostname "$HOSTNAME_FQDN"
    if grep -qE '^127\.0\.1\.1\s' /etc/hosts; then
        sed -i "s/^127\.0\.1\.1\s.*/127.0.1.1\t${HOSTNAME_FQDN}/" /etc/hosts
    else
        echo -e "127.0.1.1\t${HOSTNAME_FQDN}" >> /etc/hosts
    fi
    log_info "Hostname set to ${HOSTNAME_FQDN}."
else
    log_info "Hostname already ${HOSTNAME_FQDN} — skipped."
fi

log_step "9/9 — secrets directory (ASPS-745 relocation target, outside any repo clone)"
install -d -m 700 -o "$ASPSBOT_USER" -g "$ASPSBOT_USER" "$SECRETS_DIR"
log_info "${SECRETS_DIR} ready (mode 700, owned by ${ASPSBOT_USER})."
log_info "Phase 3/5 populate this directory with ACCESS_KEYS.env and the bot's .env (chmod 600 each) — never inside the repo clone (${CLONE_PATH:-<unset, set in Phase 3>})."

log_step "Done"
log_info "Baseline hardening applied. Before closing this session:"
log_info "  1. In a NEW terminal: ssh -p ${SSH_PORT} ${ASPSBOT_USER}@<vps-ip>"
log_info "  2. Confirm sudo works: sudo -v"
log_info "  3. Confirm UFW: sudo ufw status verbose"
log_info "  4. Only then close this root session."
log_info "Next: 02-toolchain.sh (run as root, or via sudo as ${ASPSBOT_USER})."
