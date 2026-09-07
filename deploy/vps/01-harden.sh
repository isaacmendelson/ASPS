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
#   (possibly new) SSH port with your key. This script validates the new
#   sshd config with `sshd -t`, and — ONLY when SSH_PORT is non-default —
#   disables Ubuntu 24.04's ssh.socket activation and switches to
#   ssh.service (otherwise sshd_config's "Port" is silently ignored). For
#   the default SSH_PORT=22, ssh.socket is deliberately LEFT IN PLACE (see
#   step 5's comments — a live lockout on 2026-09-07 was caused by
#   switching unconditionally, even for port 22, which isn't necessary and
#   broke sshd via a missing /run/sshd). This script asserts sshd is
#   ACTUALLY listening on SSH_PORT before it ever touches UFW — but a
#   misconfigured cloud firewall/security group in front of the box (e.g.
#   Hostinger's own panel firewall, if a non-22 SSH_PORT is used) or a
#   wrong/missing public key can still lock you out. Test before closing.
#   Root's password is left untouched by default (LOCK_ROOT=false)
#   specifically so a console/rescue path to root remains if aspsbot/sudo
#   ever breaks — see step 4.

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

log_step "4/9 — root login lockout (opt-in via LOCK_ROOT — off by default)"
# PermitRootLogin no (step 5) is the REAL control — it blocks root over SSH
# entirely, so root's password becomes irrelevant to remote access either
# way. Locking the root password on top of that is a security-review
# finding (ASPS-740 remediation, Major #2): if it runs before a WORKING
# sudo escalation path exists, it can leave the box with no path to root at
# all. `aspsbot` is created above with no password (shadow `!`) — its
# password is only ever set manually by the operator on first console
# login — so at the time this script normally runs, `sudo` has nothing to
# prompt against yet. Locking root at that moment = root locked AND sudo
# unusable = provider-rescue-only lockout.
#
# Default: LEAVE ROOT'S PASSWORD ALONE. PermitRootLogin no already closes
# the SSH vector; keeping the root password lets the Hostinger
# console/rescue path reach root if aspsbot/sudo is ever broken later.
# Set LOCK_ROOT=true in config.env — after you've set an aspsbot password
# and confirmed `sudo -v` works as aspsbot — to opt into the extra lock.
if [[ "${LOCK_ROOT,,}" != "true" ]]; then
    log_info "LOCK_ROOT=false (default) — root password left as-is. PermitRootLogin no (next step) already blocks root over SSH; console/rescue access to root is preserved intentionally. Set LOCK_ROOT=true once ${ASPSBOT_USER} has a working sudo password if you want passwd -l root too."
else
    aspsbot_pw_status="$(passwd -S "$ASPSBOT_USER" 2>/dev/null | awk '{print $2}')"
    if [[ "$aspsbot_pw_status" != "P" ]]; then
        log_warn "LOCK_ROOT=true but ${ASPSBOT_USER} has no usable password set (passwd -S: ${aspsbot_pw_status:-unknown}) — sudo would have no password to prompt against. SKIPPING root lock to avoid a total lockout (no root login AND no working sudo). Run 'passwd ${ASPSBOT_USER}' as root first, then re-run this script to have LOCK_ROOT take effect."
    elif passwd -S root 2>/dev/null | awk '{print $2}' | grep -qx 'L'; then
        log_info "root password already locked — skipped."
    else
        passwd -l root
        chage -d 0 "$ASPSBOT_USER"
        log_info "root password locked (passwd -l root). Forced ${ASPSBOT_USER} to (re)set its password on next login (chage -d 0) since sudo is now the only escalation path."
    fi
fi

log_step "5/9 — sshd hardening (drop-in, validated before reload)"

# --- drop-in precedence (ASPS-740 security remediation, Major #1) --------
# sshd is first-value-wins and reads sshd_config.d/*.conf in LEXICAL ORDER.
# Cloud images commonly ship 50-cloud-init.conf with
# "PasswordAuthentication yes", which sorts BEFORE a 99-*.conf and WINS —
# password auth stays on while sshd -t still passes. Name our drop-in to
# sort FIRST among the numbered ones, and additionally neutralize a known
# common offender if present (belt-and-suspenders — filename order alone
# is not a guarantee across every cloud image, which is why
# assert_effective_sshd_config below is the real, authoritative gate).
sshd_dropin="/etc/ssh/sshd_config.d/00-aspsbot-hardening.conf"
old_sshd_dropin="/etc/ssh/sshd_config.d/99-aspsbot-hardening.conf"
if [[ -f "$old_sshd_dropin" && "$old_sshd_dropin" != "$sshd_dropin" ]]; then
    log_info "Removing superseded ${old_sshd_dropin} (renamed to sort first among sshd_config.d drop-ins)."
    rm -f "$old_sshd_dropin"
fi

cloud_init_dropin="/etc/ssh/sshd_config.d/50-cloud-init.conf"
if [[ -f "$cloud_init_dropin" ]] && grep -qE '^[[:space:]]*(PasswordAuthentication|PermitRootLogin)\b' "$cloud_init_dropin"; then
    log_warn "Found ${cloud_init_dropin} setting PasswordAuthentication/PermitRootLogin — commenting those lines out so they cannot win over ${sshd_dropin}."
    sed -i -E 's/^([[:space:]]*(PasswordAuthentication|PermitRootLogin)\b.*)$/# \1 (disabled by 01-harden.sh, ASPS-740 — see 00-aspsbot-hardening.conf)/' "$cloud_init_dropin"
fi

dropin_changed=false
if write_if_changed "$sshd_dropin" <<EOF
# Managed by deploy/vps/01-harden.sh (ASPS-740). Do not hand-edit — re-run
# the script to change these values via config.env instead. Filename is
# 00- (not 99-) so it sorts and wins FIRST among sshd_config.d/*.conf
# drop-ins (sshd is first-value-wins) — see the comment above this block.
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
    dropin_changed=true
    log_info "Wrote ${sshd_dropin}."
fi

if [[ "$dropin_changed" == true ]]; then
    log_info "Validating sshd config (sshd -t)..."
    if ! sshd -t; then
        log_error "sshd -t FAILED against the new config. Reverting drop-in and aborting."
        rm -f "$sshd_dropin"
        exit 1
    fi
    log_info "sshd -t OK (syntax only — does not prove what actually ends up listening; see the assertions below)."
fi

# --- Ubuntu 24.04 socket activation (ASPS-740 lockout fix, 2026-09-07) ---
#
# ROOT CAUSE OF A LIVE LOCKOUT (168.231.111.91, 2026-09-07): the previous
# version of this step unconditionally disabled ssh.socket and switched to
# ssh.service for ANY SSH_PORT, including the default 22. On the real box
# that `systemctl restart ssh.service` failed and left sshd outside its
# normal service-start path, which never created /run/sshd (the privilege-
# separation runtime directory ssh.service normally creates itself via its
# own RuntimeDirectory= on a clean start) — every new SSH connection then
# reset at kex_exchange_identification. UFW correctly stayed untouched
# (the fail-safe worked), but SSH itself was down and required the
# provider's console to recover (`mkdir -p /run/sshd` + a clean
# `systemctl restart ssh.service`).
#
# Under socket activation, only sshd_config's "Port" directive is ignored
# — every OTHER directive in our 00- drop-in (PasswordAuthentication no,
# PermitRootLogin no, AllowUsers, ...) DOES apply, because socket-activated
# sshd re-reads its config for each new connection. So when SSH_PORT is the
# default 22 — i.e. exactly what ssh.socket already listens on — the whole
# switch is unnecessary: socket activation already serves 22 with our
# hardened auth, with no restart and no lockout window at all. The switch
# is only actually needed when SSH_PORT is non-default, since that's the
# one case where "Port" being ignored would matter (UFW would open a port
# sshd isn't listening on).
if [[ "$SSH_PORT" == "22" ]]; then
    if ssh_socket_activation_active; then
        log_info "SSH_PORT is 22 (the default) — leaving ssh.socket socket activation in place. Port 22 is exactly what ssh.socket already listens on, so the ignored-Port caveat doesn't apply here, and the hardened auth directives (PasswordAuthentication/PermitRootLogin/AllowUsers/...) take effect automatically on the NEXT new SSH connection without any restart. This avoids the socket-to-service switch entirely — the switch is what caused the live lockout above."
    elif [[ "$dropin_changed" == true ]]; then
        # Edge case: ssh.socket was already disabled on an earlier run (e.g.
        # SSH_PORT used to be non-default and has since been changed back to
        # 22 in config.env) — ssh.service already owns the port, so a
        # config-only change needs an explicit reload to take effect now
        # rather than waiting for some other trigger.
        log_warn "KEEP THIS SESSION OPEN. Test a new login now: ssh -p ${SSH_PORT} ${ASPSBOT_USER}@<vps-ip>"
        log_info "ssh.socket already disabled from a previous run — reloading ssh.service to apply the updated drop-in (SSH_PORT=22)."
        systemctl reload ssh.service 2>/dev/null || systemctl reload sshd.service 2>/dev/null || {
            log_error "Could not reload ssh.service/sshd.service via systemctl. Aborting before touching UFW."
            exit 1
        }
    else
        log_info "ssh.socket already disabled from a previous run and ${sshd_dropin} already correct — nothing to reload."
    fi
else
    log_warn "SSH_PORT=${SSH_PORT} (non-default) — sshd_config's Port directive is silently ignored while ssh.socket owns the listener, so switching to ssh.service is required for the custom port to actually take effect. See config.env.example / README.md: a non-22 port must also be opened in Hostinger's own panel firewall, separate from UFW."
    socket_switched=false
    if ssh_socket_activation_active; then
        systemctl disable --now ssh.socket >/dev/null 2>&1 || true
        systemctl reset-failed ssh.service ssh.socket >/dev/null 2>&1 || true
        systemctl unmask ssh.service >/dev/null 2>&1 || true
        systemctl enable ssh.service >/dev/null 2>&1 || true
        socket_switched=true
    fi
    # The missing /run/sshd directory was the ACTUAL cause of the live
    # lockout above — recreate it explicitly and unconditionally, BEFORE
    # starting/restarting ssh.service, every time this branch runs (cheap
    # and idempotent even when ssh.service's own unit would normally have
    # handled it).
    install -d -m 0755 /run/sshd
    if [[ "$dropin_changed" == true || "$socket_switched" == true ]]; then
        log_warn "KEEP THIS SESSION OPEN. Test a new login now: ssh -p ${SSH_PORT} ${ASPSBOT_USER}@<vps-ip>"
        log_info "Restarting ssh.service (switched off socket activation and/or drop-in changed; a reload alone would not bind a new listener)..."
        systemctl restart ssh.service 2>/dev/null || systemctl restart sshd.service 2>/dev/null || {
            log_error "Could not restart ssh.service/sshd.service via systemctl after disabling socket activation. Aborting before touching UFW."
            exit 1
        }
    fi
    if ! systemctl is-active --quiet ssh.service && ! systemctl is-active --quiet sshd.service; then
        log_error "ssh.service/sshd.service is not active after the socket-to-service switch. Aborting before touching UFW."
        exit 1
    fi
    log_info "ssh.service confirmed active on SSH_PORT=${SSH_PORT}."
fi

# --- authoritative post-merge/post-listen gate (ASPS-740 remediation) ----
# Run on EVERY invocation (not just when something changed above) — cheap,
# and catches drift such as an unattended-upgrade reintroducing
# ssh.socket or a package update dropping a new sshd_config.d/*.conf that
# wins over ours. MUST pass before step 6 touches UFW — a box that fails
# here and still gets its firewall opened could be left reachable only on
# a port/config UFW denies, or with password auth silently back on.
log_info "Asserting effective sshd config and listening port before opening UFW..."
if ! assert_effective_sshd_config; then
    exit 1
fi
if ! assert_sshd_listening "${SSH_PORT}"; then
    exit 1
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
