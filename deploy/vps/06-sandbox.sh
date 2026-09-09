#!/usr/bin/env bash
# ASPS-764 -- sub-task of ASPS-763 (see ADR-005): enable bubblewrap on the
# VPS so the Telegram CEO bot's Claude Agent SDK sandbox (`sandbox.enabled`,
# Linux backend = bwrap) can run as ${ASPSBOT_USER}.
#
# Installs `bubblewrap` and a scoped AppArmor profile for `/usr/bin/bwrap`
# that grants it the `userns` permission Ubuntu 24.04 requires (see
# "AppArmor userns resolution" below). Does NOT touch
# kernel.apparmor_restrict_unprivileged_userns (stays at its distro default,
# 1) -- the scoped profile is the PREFERRED fix; the global sysctl is a
# documented fallback this script does not apply. Does NOT start, stop, or
# restart telegram-ceo.service -- the RestrictNamespaces= scoping this
# feature also needs lives in telegram-ceo.service itself (rendered by
# 05-service.sh) and only takes effect on the next service (re)start.
#
# ASPS-779 fold-in: also installs `socat` -- the SDK's own sandbox schema
# lists a separate `socatPath` option alongside `bwrapPath` (see
# node_modules/@anthropic-ai/claude-agent-sdk/sdk.mjs), because the
# sandbox's network proxy (used to enforce network.allowedDomains egress
# control for sandboxed Bash) shells out to `socat`, not just `bwrap`. It
# was previously installed by hand on the live box outside this script --
# provisioning drifting from the real box is exactly the gap this fold-in
# closes, so a fresh box provisioned from this script alone has everything
# the sandbox needs.
#
# Runs as root. Idempotent -- safe to re-run.
#
# See docs/cloud/VPS_TELEGRAM_HARDENING.md "bubblewrap sandbox enablement"
# for the full decision record, the validated acceptance evidence from the
# live box, and the AppArmor-profile-vs-sysctl tradeoff discussion.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

require_root
require_ubuntu_2404
load_config "$SCRIPT_DIR"

apparmor_profile_target="/etc/apparmor.d/bwrap"

log_step "1/5 -- install bubblewrap"
if package_installed bubblewrap; then
    log_info "bubblewrap already installed ($(dpkg-query -W -f='${Version}' bubblewrap))."
else
    apt-get update -y
    apt-get install -y bubblewrap
    log_info "bubblewrap installed ($(dpkg-query -W -f='${Version}' bubblewrap))."
fi

if [[ ! -x /usr/bin/bwrap ]]; then
    log_error "/usr/bin/bwrap not found after installing the bubblewrap package -- aborting."
    exit 1
fi

log_step "2/5 -- install socat (ASPS-779: SDK sandbox network proxy)"
# The Claude Agent SDK sandbox's network proxy (enforces
# network.allowedDomains egress control for sandboxed Bash) shells out to
# `socat`, independently of `bwrap` -- see the top-of-file comment. Mirrors
# the bubblewrap install above: idempotency check, install, then verify the
# binary is actually on PATH afterward rather than trusting the package
# install silently succeeded.
if package_installed socat; then
    log_info "socat already installed ($(dpkg-query -W -f='${Version}' socat))."
else
    apt-get update -y
    apt-get install -y socat
    log_info "socat installed ($(dpkg-query -W -f='${Version}' socat))."
fi

if ! command -v socat >/dev/null 2>&1; then
    log_error "socat not found on PATH after installing the socat package -- aborting."
    exit 1
fi
log_info "socat verified on PATH: $(command -v socat)"

log_step "3/5 -- AppArmor userns resolution: scoped profile for /usr/bin/bwrap"
# Ubuntu 24.04 mediates *unprivileged* user-namespace creation with AppArmor
# (kernel.apparmor_restrict_unprivileged_userns=1, the distro default -- left
# untouched by this script). An unconfined process (no AppArmor profile
# attached, which is the default for anything not explicitly profiled) is
# blocked from calling unshare(CLONE_NEWUSER) unless it is confined by a
# named profile that includes a `userns,` rule. The PREFERRED, scoped fix
# (over the global `kernel.apparmor_restrict_unprivileged_userns=0` sysctl
# fallback, which would remove the restriction box-wide for every
# unconfined process, not just bwrap) is to give /usr/bin/bwrap its own
# named profile that grants exactly that one extra permission.
#
# This profile follows the EXACT pattern Ubuntu already ships on this box
# for the identical restriction: /etc/apparmor.d/lxc-usernsexec
# (flags=(unconfined) + `userns,`) -- a profile whose only purpose is to
# name an otherwise-unconfined binary so the kernel's "is this task
# profile-confined?" userns check passes, without adding any file/network
# mediation bwrap doesn't already provide for itself via the namespaces it
# creates. This is a standard, documented Ubuntu/AppArmor workaround for
# this exact 24.04 restriction (used for bwrap/flatpak, lxc-usernsexec,
# lxc-unshare, chrome-sandbox, etc. across the ecosystem), not a bespoke
# hack.
if write_if_changed "$apparmor_profile_target" <<'EOF'
# ASPS-764 -- scoped AppArmor profile granting /usr/bin/bwrap permission to
# create unprivileged user namespaces under Ubuntu 24.04's
# kernel.apparmor_restrict_unprivileged_userns=1 restriction.
#
# Pattern is Ubuntu's own precedent already present on this distro for the
# same restriction: /etc/apparmor.d/lxc-usernsexec (flags=(unconfined) +
# userns,). bwrap performs ITS OWN sandboxing via Linux namespaces/seccomp
# inside the userns it creates -- this profile does not need to (and
# deliberately does not) mediate bwrap's filesystem/network access itself;
# it only exists to give the unconfined-equivalent process a named AppArmor
# label so the kernel's userns-creation check (which requires ANY
# non-bare-unconfined label, not blanket permission) passes, while granting
# the one extra permission (userns,) that a truly unconfined process would
# already have had implicitly before this Ubuntu 24.04 restriction existed.
abi <abi/4.0>,
include <tunables/global>

profile bwrap /usr/bin/bwrap flags=(unconfined) {
  userns,

  # Site-specific additions and overrides. See local/README for details.
  include if exists <local/bwrap>
}
EOF
then
    log_info "${apparmor_profile_target} written -- loading with apparmor_parser -r."
    apparmor_parser -r "$apparmor_profile_target"
    log_info "AppArmor profile loaded."
else
    log_info "${apparmor_profile_target} already up to date -- skipped (re-parsing anyway is cheap and safe on re-run, but unnecessary here)."
fi

log_step "4/5 -- confirm the box-wide sysctl restriction was NOT relaxed (fallback not applied)"
current_restrict_userns="$(sysctl -n kernel.apparmor_restrict_unprivileged_userns 2>/dev/null || echo unknown)"
log_info "kernel.apparmor_restrict_unprivileged_userns=${current_restrict_userns} (left at distro default by this script -- the scoped /usr/bin/bwrap profile above is the fix, not this sysctl)."
if [[ "$current_restrict_userns" != "1" ]]; then
    log_warn "Expected kernel.apparmor_restrict_unprivileged_userns=1 (distro default) but found '${current_restrict_userns}'. Something outside this script already changed it -- verify intentionally, this script does not manage that sysctl."
fi

log_step "5/5 -- smoke test: userns+mnt+pid bwrap sandbox as ${ASPSBOT_USER}"
# Read-only, non-destructive: unshares user+pid, ro-binds the whole
# filesystem, and runs `true`. Exercises exactly the AppArmor path fixed
# above (unshare(CLONE_NEWUSER) as an unprivileged, non-root user) without
# depending on ${SECRETS_DIR} or the repo clone existing yet, so this script
# can run standalone / before 03-clone.sh.
if id -u "$ASPSBOT_USER" >/dev/null 2>&1; then
    if runuser -u "$ASPSBOT_USER" -- bwrap --unshare-user --unshare-pid --ro-bind / / --die-with-parent true; then
        log_info "bwrap userns smoke test PASSED as ${ASPSBOT_USER} (unshare-user + unshare-pid succeeded)."
    else
        log_error "bwrap userns smoke test FAILED as ${ASPSBOT_USER} -- the AppArmor profile may not have loaded correctly, or the box-wide sysctl changed underneath this script. See docs/cloud/VPS_TELEGRAM_HARDENING.md troubleshooting notes."
        exit 1
    fi
else
    log_warn "User ${ASPSBOT_USER} does not exist yet (run 01-harden.sh first) -- skipped the smoke test. bubblewrap + the AppArmor profile are installed regardless."
fi

log_step "Done"
log_info "bubblewrap installed and /usr/bin/bwrap can create unprivileged user+mount+pid namespaces as ${ASPSBOT_USER}."
log_info "socat installed ($(command -v socat)) -- required by the SDK sandbox's network proxy (ASPS-779)."
log_info "Reminder: telegram-ceo.service's RestrictNamespaces= (scoped to 'user pid mnt' in the template / its drop-in) only takes effect on the unit's next (re)start -- this script deliberately does not start/restart the service."
log_info "For the full realistic-sandbox acceptance test (secrets masked, ambient git-push credential unreachable, network egress kept, repo bind-mount writable), see docs/cloud/VPS_TELEGRAM_HARDENING.md 'bubblewrap sandbox enablement'."
