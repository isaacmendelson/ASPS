# DevOps Decisions

Verify each decision against current files before relying on it.

## Docker architecture

- Analyzer runs as isolated sidecar container communicating with backend via
  Unix domain socket (`/run/asps-analyzer/analyzer.sock`). Backend contains
  only a thin Python IPC client (`Analyzers/analyzer-client/analyze.py`);
  Playwright/Chromium and hostile content stay in the analyzer container.
- Analyzer container starts as root for iptables egress firewall setup, then
  drops to non-root (UID 10001) via `setpriv` before running uvicorn.
  Capabilities `NET_ADMIN`, `SETUID`, `SETGID`, `SETPCAP` are granted at
  start and dropped after firewall init.
- Analyzer egress network (`analyzer-egress`) has ICC disabled — analyzer
  cannot reach other containers or private networks. Only public internet
  egress is allowed.
- Keycloak realm config lives in a Docker volume (`keycloak_data`), not in a
  realm-export file. This is a known reproducibility gap.

## Ports

- MySQL exposed on 3307 (not 3306) to avoid host collision.
- Keycloak on 8081 (not 8080) to avoid collision with WebApi and desktop
  agent's WebSocket range.
- WebApi on 8080 (HTTP only in dev; HTTPS termination is a future concern).

## Environment

- `CQRS_SHARED_SECRET` is the only mandatory external env var for compose.
  It uses `${VAR:?msg}` syntax to fail fast if missing.

## Azure Container Apps — `az containerapp` CLI extension bug (2026-08-22, ASPS-727/728)

- `az containerapp update --yaml`, `--replace-env-vars`, and `--set-env-vars` (extension
  `1.3.0b4`, latest available) silently drop every plain (non-`secretRef`) env var value
  on write — only `secretRef`-backed entries survive. Reproduced twice independently.
  The only proven-safe CLI pattern is the existing CI/CD one: export YAML, `sed`-patch
  *only* the image tag string, re-apply — do not restructure env/ingress via the CLI.
- For anything else (new env vars, ingress port changes, etc.), use
  `az rest --method patch --url ".../Microsoft.App/containerApps/{name}?api-version=2025-07-01"`
  with a full `properties.configuration` + `properties.template` JSON body instead.
  Fetch real secret values first via `az containerapp secret list --show-values` (ARM
  PATCH does not auto-preserve secrets like the CLI does) — keep them in ephemeral
  scratch files only, never logged or committed. Strip these fields from `show` output
  before PATCHing (rejected as unknown at this api-version): `targetPortHttpScheme`
  (ingress), `revisionTransitionThreshold`, `targetLabel` (configuration), `imageType`
  (per container), `customMetricsSettings` (template). Poll
  `properties.provisioningState` to `Succeeded` before issuing a new PATCH if a prior
  operation may still be in flight (`409 ContainerAppOperationInProgress` otherwise).
- Container Apps TCP ingress `external` is an all-or-nothing switch tied to whichever
  port is the *main* `ingress.targetPort` — you cannot mark an `additionalPortMappings`
  entry `external: true` while the main port's own `external` is `false`
  (`ContainerAppInvalidIngressAdditionalPortMappings`). To get one port internal and
  others external on the same app, make one of the *external* ports the main ingress
  port and move the internal one into `additionalPortMappings` with `external: false`.

## Keycloak health probes — management interface port (2026-08-25, ASPS-735)

- Keycloak 26+ serves `/health/*` (and `/metrics`) on a separate **management interface**,
  port **9000** by default — not the main HTTP port (8080) used for OIDC/admin traffic. This is
  only active once the health subsystem is explicitly enabled via `KC_HEALTH_ENABLED=true`.
  Container Apps probes execute directly against the container's declared port (no ingress
  exposure required), so the fix is: set `KC_HEALTH_ENABLED=true` (+ explicit
  `KC_HTTP_MANAGEMENT_PORT=9000`) and point `probes[].httpGet.port` at 9000, not 8080.
- `ca-keycloak-dev` was originally deployed with probes pointing at the correct paths but port
  8080, and without `KC_HEALTH_ENABLED` — every probe request 404'd, exhausting the startup
  probe's `failureThreshold` (50 × 10s = 530s) and causing an 810+ restart CrashLoopBackOff.
  Fixed via ARM PATCH (env var addition — not safe via `az containerapp update`, see the CLI
  bug entry above). See AD-11 in `docs/cloud/AZURE_ARCHITECTURE.md`.
