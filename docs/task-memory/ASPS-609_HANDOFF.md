# ASPS-609 Handoff

## Task

- Jira: ASPS-609
- Exact title: `[CODE REVIEW] Add SSRF protection and isolated Chromium execution to Basic URL Analyzer`
- Implementing agent: `analyzer-ai`
- Status: PRE-QA READY after QA FAIL remediation; no commit and no Jira
  completion transition performed.

## Acceptance and security checklist

- [x] Reject loopback, RFC1918/private, link-local/cloud metadata, unspecified,
  reserved/documentation and all other non-global IPv4/IPv6 destinations.
- [x] Resolve DNS and reject a hostname when any answer is non-public.
- [x] Validate the submitted URL before Chromium navigation.
- [x] Apply the same policy to every browser request, including redirects.
- [x] Apply context-wide interception to popup/new-page requests.
- [x] Intercept and reject unsafe WebSocket destinations before connect.
- [x] Revalidate the final page destination.
- [x] Detect DNS rebinding to a private destination on repeated resolution.
- [x] Prevent DNS rebinding before connect by resolving once in the filtering
  proxy and opening the outbound socket to the exact validated IP.
- [x] Validate Chromium's reported connected peer address as defense in depth.
- [x] Reject URL credentials and non-HTTP(S) navigation.
- [x] Restore Chromium sandbox, web security, CSP, TLS verification and
  origin/site/process isolation defaults.
- [x] Block service workers, downloads and browser permissions in the scraper
  context.
- [x] Document the mandatory production egress/container isolation boundary.
- [x] Run Chromium/Analyzer in a dedicated non-root hardened container, with no
  Backend keys or shared Backend network; use a Unix-domain socket for IPC.
- [x] Allow Chromium's expected loopback connection to the trusted pinned proxy
  without treating the proxy peer as the remote destination.
- [x] Block unsafe routed WebSockets without a synchronous Playwright `close()`
  call or any server TCP connection.
- [x] Enforce container OUTPUT firewall denies and process/memory/CPU limits.

## Implementation

- `Analyzers/basic-url-analyzer/utils/validators.py`
  - Added `URLSecurityPolicy` and `UnsafeURLException`.
  - Uses `socket.getaddrinfo` and `ipaddress.is_global`; all DNS answers must be
    globally routable.
  - Supports repeated validation and Chromium peer-address validation.
  - `URLValidator.validate` now applies the security policy before accepting a URL.
- `Analyzers/basic-url-analyzer/scrapers/playwright_scraper.py`
  - Validates before navigation, on every routed request/redirect, on the final
    URL and against Chromium's reported server address.
  - Removed `--no-sandbox`, `--disable-web-security` and
    `--disable-features=IsolateOrigins,site-per-process`.
  - Disabled CSP/TLS bypasses, service workers, downloads and permissions.
  - Ensures browser contexts close in `finally`.
- `Analyzers/basic-url-analyzer/scrapers/egress_proxy.py`
  - Added the mandatory loopback filtering proxy. It resolves and validates the
    destination, then passes only the chosen numeric IP to `create_connection`.
  - Chromium's implicit loopback proxy bypass is explicitly removed.
- `Analyzers/basic-url-analyzer/tests/test_ssrf_security.py`
  - Added 19 regression/security tests for literals, DNS, mixed answers,
    metadata, rebinding, redirects, credentials, connected peer and launch flags.
- `Analyzers/basic-url-analyzer/README.md`
  - Documented required unprivileged execution, egress denial, proxy/network
    policy, read-only/disposable filesystem and resource/deadline limits.

## TDD evidence

The initial DNS/IP and Chromium changes were implemented before the new mandatory
TDD instruction arrived. Their tests are regression coverage added afterward, so
there is no honest Red-first run for that initial slice. Root/CEO approval of this
documented exception is required before QA.

One remaining refactoring slice was performed Red → Green:

- Red command:
  - `.venv\Scripts\python.exe -m pytest tests/test_ssrf_security.py::test_scraper_revalidates_redirect_url_through_shared_policy --tb=short -q`
  - Result: 1 failed, 0 passed, 0 skipped.
  - Intended failure: `AttributeError` because `_authorize_browser_url` did not yet
    exist.
- Green/refactor:
  - Added `_authorize_browser_url` as the single browser URL-policy seam and routed
    initial, redirect/request and final URL checks through it.
  - Replaced a private `ipaddress` type annotation with public IPv4/IPv6 types.

### QA FAIL remediation Red → Green

- Popup/WebSocket and pre-connect egress Red:
  - `pytest tests/test_egress_proxy.py` initially failed collection because the
    pinned proxy did not exist.
  - After the proxy seam existed, the negative range run produced 3 intended
    failures: IPv4 multicast `224/4` and IPv6 multicast were accepted by
    `ipaddress.is_global`.
- Green:
  - Context-level HTTP request and WebSocket interception covers current pages,
    popups and future pages.
  - Popup metadata exploit is aborted and private popup WebSocket is closed
    without `connect_to_server`.
  - Proxy tests prove the hostname is never passed to the outbound connect call
    and a private rebinding answer results in zero connect attempts.
  - Multicast, reserved, unspecified, loopback, link-local and private properties
    are now checked explicitly.
- Deployment Red:
  - 3/3 deployment contract tests failed: no dedicated image/service and Backend
    still contained Chromium/full Analyzer.
- Deployment Green:
  - 3/3 pass after separate non-root container, Unix-socket IPC, key/network
    separation and Backend image reduction.

### Second QA FAIL remediation Red → Green

- Red command:
  - `.venv\Scripts\python.exe -m pytest tests/test_analyzer_deployment.py tests/test_egress_proxy.py --tb=short -q`
  - Result: 4 failed, 13 passed.
  - Intended failures: missing injectable pinned-connect runtime seam, blocking
    WebSocket `close()` still called, and missing firewall/resource contracts.
- Green:
  - Real focused runtime test starts an HTTP origin and the real filtering proxy.
    A public hostname succeeds through the proxy while the connect factory sees
    only the validated numeric public IP. Rebinding the resolver to loopback
    fails and produces zero additional TCP connections.
  - Chromium loopback `response.server_addr` is accepted only while the mandatory
    pinned proxy is active; the proxy, not the remote origin, is that peer.
  - Unsafe WebSocket routes install a local drain handler and return in under
    100ms without calling `connect_to_server()` or synchronous `close()`.
  - Container entrypoint installs IPv4/IPv6 OUTPUT rejects for private, metadata,
    link-local, reserved and multicast ranges, then drops `NET_ADMIN` with
    `setpriv` before Python/Chromium. Compose adds PID, memory and CPU limits.

### Third QA FAIL remediation Red → Green

- Red command:
  - `.venv\Scripts\python.exe -m pytest tests/test_egress_proxy.py::test_multicast_reserved_and_special_purpose_addresses_are_explicitly_blocked tests/test_analyzer_deployment.py::test_analyzer_container_receives_no_backend_keys_or_internal_network --tb=short -q`
  - Result: 2 failed, 8 passed.
  - Intended failures: deprecated IPv6 site-local `fec0::/10` was accepted by the
    validator and absent from the container firewall contract.
- Green:
  - `URLSecurityPolicy` explicitly rejects IPv6 `is_site_local`.
  - The `ip6tables` OUTPUT deny list explicitly includes `fec0::/10`.

### Fourth QA FAIL remediation (2026-07-29)

- Blocker — `test_analyzer_has_dedicated_non_root_hardened_container` asserted
  `USER analyzer` in `Dockerfile.analyzer`, but the container intentionally
  starts as root (for iptables) and drops to UID 10001 via `setpriv` in the
  entrypoint. The assertion was wrong, not the Dockerfile.
  - Fixed: test now asserts `useradd`+`10001` in Dockerfile, `USER root` absent,
    `setpriv` and `--reuid=10001` in the entrypoint script.
- Major — 6to4 IPv6 addresses (`2002::/16`) embed IPv4 in bits 16-47.
  `2002:7f00:0001::` routes to `127.0.0.1`. Python's `ipaddress.is_global`
  returns `True` for these addresses, so they bypassed the validator.
  - Fixed in `utils/validators.py`: explicit `ip in IPv6Network("2002::/16")`
    check added to `_validate_ip`.
  - Fixed in `scripts/analyzer-entrypoint.sh`: `2002::/16` added to the
    `ip6tables` OUTPUT deny list.
  - Tests added: 3 parametrized 6to4 literals in `test_ssrf_security.py`
    (`2002:7f00:0001::`, `2002:c0a8:0101::`, `2002:a9fe:a9fe::`) and 2 in
    `test_egress_proxy.py`; 1 contract assertion in `test_analyzer_deployment.py`.
- `close()` resource leak review: `_fetch_with_ua` closes `context` in
  `finally`; `fetch()` calls `self.close()` in `finally` covering browser,
  playwright and egress_proxy in all error/timeout/success paths. No leak found.

## Verification

- `python -m pytest tests/test_analyzer_deployment.py tests/test_egress_proxy.py tests/test_ssrf_security.py -v`
  - 42 passed, 0 failed, 0 skipped. (Was 37 before 6to4 fixes added 5 tests.)
- `python -m pytest tests/ --ignore=tests/test_real_sites.py -q`
  - 341 passed, 3 failed, 4 skipped; exit code 1.
  - The three failures are pre-existing/unrelated baseline defects: two
    Ollama-disabled configuration expectations and one stored ML-model false
    negative. ASPS-609 changes no Ollama, ML classifier, model, training data or
    scoring file; those failure-owning files are untouched.
  - The complete ASPS-609 security/deployment suite is independently green.
- `tests/test_real_sites.py`
  - Removed a test-module `sys.stdout` replacement which broke reliable pytest
    capture; the file contains no pytest test cases.

## Remaining deployment constraint

Docker itself is unavailable in the current environment, so `docker compose
config` and an image build could not be executed. Static deployment contract
tests validate the required service/image properties. CI or a Docker-capable host
must run `docker compose config --quiet` and build both images before release.

## Continuation point

All QA findings from the fourth review are remediated and green. Ready for
independent QA re-review. On QA PASS, commit and close ASPS-609 in Jira.
- `Analyzers/basic-url-analyzer/tests/test_egress_proxy.py`
  - Added 13 tests for pre-connect DNS pinning, private-connect fail-closed,
    multicast/reserved/special-purpose ranges, context-wide popup interception
    and WebSocket blocking.
- `Analyzers/basic-url-analyzer/tests/test_analyzer_deployment.py`
  - Added 3 deployment contract checks for non-root hardening, key/network
    separation and removal of Chromium from the Backend image.
- `Dockerfile.analyzer`, `docker-compose.yml`, `Dockerfile.backend`
  - Added a dedicated UID 10001 Analyzer service with read-only filesystem,
    dropped capabilities, no-new-privileges, tmpfs and isolated egress network.
  - Backend no longer contains the Analyzer/browser; it mounts only the private
    Analyzer Unix socket.
- `Analyzers/analyzer-client/analyze.py`,
  `ASPSBackend14_J/ASPSBackend/appsettings.Docker.json`
  - Added a standard-library Unix-socket CLI compatibility client so the existing
    Backend process contract reaches the isolated service without a shared network.
- `Analyzers/basic-url-analyzer/api.py`
  - Added the Unix-socket-only raw result endpoint used by the compatibility client.
