#!/bin/sh
set -eu

# The Analyzer must never reach host, private, metadata, multicast or reserved
# networks even if application validation is bypassed.
iptables -A OUTPUT -o lo -j ACCEPT
ip6tables -A OUTPUT -o lo -j ACCEPT

for cidr in \
    0.0.0.0/8 10.0.0.0/8 100.64.0.0/10 127.0.0.0/8 \
    169.254.0.0/16 172.16.0.0/12 192.0.0.0/24 192.0.2.0/24 \
    192.168.0.0/16 198.18.0.0/15 198.51.100.0/24 203.0.113.0/24 \
    224.0.0.0/4 240.0.0.0/4; do
    iptables -A OUTPUT -d "$cidr" -j REJECT
done

for cidr in \
    ::/128 ::1/128 100::/64 2001:db8::/32 2002::/16 fc00::/7 fec0::/10 fe80::/10 ff00::/8; do
    ip6tables -A OUTPUT -d "$cidr" -j REJECT
done

# Drop root + firewall capability before starting Python/Chromium.
exec setpriv \
    --reuid=10001 --regid=10001 --clear-groups \
    --bounding-set=-net_admin,-setuid,-setgid,-setpcap \
    --inh-caps=-all \
    --ambient-caps=-all \
    --no-new-privs \
    "$@"
