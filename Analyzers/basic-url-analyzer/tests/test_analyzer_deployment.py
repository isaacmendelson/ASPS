"""Contract checks for the production Analyzer isolation boundary."""

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]


def test_analyzer_has_dedicated_non_root_hardened_container():
    dockerfile = (REPO_ROOT / "Dockerfile.analyzer").read_text(encoding="utf-8")
    compose = (REPO_ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    entrypoint = (
        REPO_ROOT
        / "Analyzers"
        / "basic-url-analyzer"
        / "scripts"
        / "analyzer-entrypoint.sh"
    ).read_text(encoding="utf-8")

    # The Dockerfile does NOT use `USER analyzer`; instead it starts as root
    # to configure iptables, then the entrypoint drops to UID 10001 via setpriv.
    # Verify the user account exists (useradd) and the privilege-drop is in the
    # entrypoint script rather than a static Dockerfile USER directive.
    assert "useradd" in dockerfile and "10001" in dockerfile
    assert "USER root" not in dockerfile, (
        "Dockerfile must never add an explicit USER root directive; root is "
        "only the implicit build default, used for iptables setup at runtime"
    )
    assert "analyzer-entrypoint.sh" in dockerfile
    assert "setpriv" in entrypoint
    assert "--reuid=10001" in entrypoint

    assert "analyzer:" in compose
    assert "read_only: true" in compose
    assert "cap_drop:" in compose and "- ALL" in compose
    assert "no-new-privileges:true" in compose
    assert "tmpfs:" in compose
    assert "pids_limit:" in compose
    assert "mem_limit:" in compose
    assert "cpus:" in compose


def test_analyzer_container_receives_no_backend_keys_or_internal_network():
    compose = (
        (REPO_ROOT / "docker-compose.yml")
        .read_text(encoding="utf-8")
        .replace("\r\n", "\n")
    )
    analyzer_block = compose.split("\n  analyzer:", 1)[1].split("\nvolumes:", 1)[0]

    assert "curve_keys" not in analyzer_block
    assert "curve_public_keys" not in analyzer_block
    assert "CQRS__" not in analyzer_block
    assert "asps-network" not in analyzer_block
    assert "analyzer-egress" in analyzer_block
    assert "NET_ADMIN" in analyzer_block
    entrypoint = (REPO_ROOT / "Analyzers" / "basic-url-analyzer" / "scripts" / "analyzer-entrypoint.sh").read_text(encoding="utf-8")
    assert "169.254.0.0/16" in entrypoint
    assert "10.0.0.0/8" in entrypoint
    assert "fc00::/7" in entrypoint
    assert "fec0::/10" in entrypoint
    assert "ff00::/8" in entrypoint
    # 6to4 addresses (2002::/16) embed IPv4; must be blocked at the firewall
    # layer as defense-in-depth alongside the validator's explicit check.
    assert "2002::/16" in entrypoint
    assert entrypoint.index("-o lo -j ACCEPT") < entrypoint.index("127.0.0.0/8")


def test_backend_image_contains_only_analyzer_ipc_client_not_browser_worker():
    dockerfile = (REPO_ROOT / "Dockerfile.backend").read_text(encoding="utf-8")

    assert "COPY Analyzers/analyzer-client/" in dockerfile
    assert "COPY Analyzers/basic-url-analyzer/" not in dockerfile
    assert "playwright install chromium" not in dockerfile
