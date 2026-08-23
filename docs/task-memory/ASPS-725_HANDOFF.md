# ASPS-725 — Split Backend out of sidecar (parent) / ASPS-726 — Practical CURVE test

**JIRA:** ASPS-725 (parent), ASPS-726 (CURVE test), ASPS-727 (standalone Backend),
ASPS-728 (WebApi endpoint switch), ASPS-729 (sidecar removal), ASPS-730 (CI/CD update)
**Agent:** devops
**Date:** 2026-08-22 (ASPS-726/727/728), 2026-08-23 (ASPS-729/730)
**Branch:** `asps-725-remove-sidecar-update-cicd` (new branch for ASPS-729/730; the earlier
`asps-725-expose-backend-tcp-ports` branch used for ASPS-726/727/728 was already merged to
`main` via PR #32, commit `9f834bb`)
**Status:**
- ASPS-726 (CURVE test) — DONE. CURVE handshake over app-name internal TCP **SUCCEEDS**.
- ASPS-727 (reactivate `ca-backend-dev` standalone) — **DONE, verified**.
- ASPS-728 (WebApi → Backend via app-name TCP) — **DONE, verified**.
- ASPS-729 (remove Backend sidecar from `ca-webapi-dev`) — **DONE, verified** (2026-08-23).
  The blocker below was fixed in PR #32 (commit `4b2460e`) before this session started, then
  a *second*, previously-unknown blocker was found and fixed in this session (deployed image
  predated the fix) — see "ASPS-729 — DONE" section below.
- ASPS-730 (update CI/CD pipeline for separate Backend deploy) — **DONE**.
- JIRA: ASPS-729 and ASPS-730 transitioned To Do → In Progress (labels already `devops`).
  Not yet transitioned to In Review — no PR opened yet in this session; next agent/orchestrator
  should open the PR from `asps-725-remove-sidecar-update-cicd` and complete the QA gate per
  `.claude/rules/task-workflow.md` before transitioning further.

---

## ASPS-727 — Reactivate `ca-backend-dev` as standalone Container App — DONE

**Result:** `ca-backend-dev` is now an active, standalone Container App running Backend,
independent of the `ca-webapi-dev` sidecar. Verified healthy.

### Final configuration

| Setting | Value |
|---|---|
| Image | `acraspsisaacdev.azurecr.io/asps-backend:20260819-0604ba5` (same tag as the sidecar) |
| CPU / Memory | 1.0 / 2Gi |
| Managed identity | `id-asps-dev` (ACR pull) — already assigned, unchanged |
| Volume | Azure Files `curvekeys` → `/keys` (read-write) — unchanged |
| Secrets | `db-connection-string`, `cqrs-shared-secret` — already existed on this app, values untouched |

**Ingress** (TCP, VNet-integrated environment):

| Port | Role | External |
|---|---|---|
| 50001 | Alert listener (device → backend) | **true** — set as the *main* ingress `targetPort` |
| 50002 | Notification publisher | **true** — `additionalPortMappings` |
| 5556 | CQRS gateway | **false** — `additionalPortMappings` |
| 5555 | Legacy NetMQ (AD-4) | **not exposed at all** — no mapping |

**Important discovery — ingress `external` is an all-or-nothing app-level switch, not
purely per-port:** Azure rejects any `additionalPortMappings[].external: true` entry if
the container app's main `ingress.external` is `false`
(`ContainerAppInvalidIngressAdditionalPortMappings`). This means you cannot have "main
port internal, additional port external" while the main port stays as the literal
`external:false` entry — the *first* attempt (main port 5556 external:false + 50001/50002
in additionalPortMappings marked external:true) was rejected for this reason. **Fix:**
made 50001 the *main* ingress port (`external: true`), and moved 5556 into
`additionalPortMappings` as `external: false`, with 50002 also in
`additionalPortMappings` as `external: true`. This achieves the required exposure matrix
(50001 external, 50002 external, 5556 internal, 5555 absent) — which port is "main"
vs "additional" doesn't matter functionally, only the per-port `external` flags do. This
correction should be applied to AD-3 wording when ASPS-731 (docs) is done.

Env vars replicated 1:1 from the sidecar's `backend` container
(`ConnectionStrings__DefaultConnection`, `Python__ExecutablePath`,
`Python__AnalyzersFolderPath`, `NetMQ__BusinessEndpoint=tcp://*:5555`,
`NetMQ__RealTimeListenerPort=50001`, `NetMQ__NotificationPublisherPort=50002`,
`Security__CurveEnabled=true`, `Security__KeysFilePath`,
`Security__ServerPublicKeyFilePath`, `CQRS__BindEndpoint=tcp://*:5556`,
`CQRS__SharedSecret`, `ASPNETCORE_ENVIRONMENT=Docker`).

**Added per task instruction (AD-3 TCP keepalive), but currently INERT — see flag below:**
`NetMQ__TcpKeepalive=true`, `NetMQ__TcpKeepaliveIdle=60`, `NetMQ__TcpKeepaliveInterval=30`.

> **Flag (no silent side-fix):** grepped the entire `ASPSBackend14_J` solution for any
> code that reads a `TcpKeepalive*` configuration key or sets NetMQ's
> `Options.TcpKeepalive` / `TcpKeepaliveIdle` / `TcpKeepaliveInterval` socket options
> (`NetMQAlertIngress.cs`, `NetMQNotificationEgress.cs`, `NetMQCqrsTransport.cs`,
> `NetMQMessageProcessor.cs`) — **no such code exists**. These three env vars are set on
> the Container App exactly as the task instructed, but they currently have **zero
> effect** — AD-3 already flagged this as "code change ~4 lines per side", and that code
> change was never made. This is an application-code gap, not something DevOps can/should
> fix directly (routes to Backend implementer if/when the ~4-day idle-connection drop
> described in AD-3 becomes an actual observed problem).

### Verification

```
Revision: ca-backend-dev--0000008 (active)
healthState: Healthy
runningState: RunningAtMaxScale
replica container: ready=true, restartCount=0, runningState=Running
```

---

## ASPS-728 — WebApi → Backend via internal app-name TCP — DONE

Updated `ca-webapi-dev`'s `webapi` container env vars (only; sidecar `backend` container
untouched, still running as a safety net per instructions):

| Variable | Before | After |
|---|---|---|
| `CQRS__Endpoint` | `tcp://localhost:5556` | `tcp://ca-backend-dev:5556` |
| `NetMQ__BusinessEndpoint` | `tcp://localhost:5555` | `tcp://ca-backend-dev:5555` (legacy/unused channel, AD-4 — updated for parity only) |
| `NetMQ__AlertListenerEndpoint` | `tcp://localhost:50001` | `tcp://ca-backend-dev:50001` (this config key is **dead** — grepped `WebApi/Program.cs`, nothing reads `NetMQ:AlertListenerEndpoint`; updated for consistency/documentation only, no functional effect either way) |

All other env vars (Keycloak, CORS, Security__*, secrets) untouched.

### Verification

- New revision `ca-webapi-dev--0000005` deployed: both `webapi` and `backend` (sidecar)
  containers `ready:true`, `restartCount:0`, `runningState:Running`.
- WebApi startup log: `✓ CQRS Client configured: tcp://ca-backend-dev:5556` — no
  connection errors on startup.
- Standalone `ca-backend-dev` log at the same time: `CQRS Gateway started on tcp://*:5556
  with CURVE and authenticated envelopes` — healthy, listening.
- `https://ca-webapi-dev.../` → HTTP 302 (redirect to Keycloak login — expected, proves
  the app is serving requests normally, not crash-looping).
- `https://ca-keycloak-dev.../realms/asps/.well-known/openid-configuration` → HTTP 200
  (SSO/OIDC discovery unaffected — no Keycloak config was touched).
- **Not independently re-verified with an authenticated session** (no test-user
  credentials available in this session) — relying on (a) the clean startup logs above,
  (b) ASPS-726's already-proven CURVE handshake success over this exact
  `ca-backend-dev:5556` app-name/port combination, and (c) both containers passing health
  checks with zero restarts. `az containerapp exec` hit the same rate limit documented in
  ASPS-726 (429, `retry-after: 600`) partway through additional verification attempts, so
  no further exec-based probes were run this session — if deeper verification is wanted,
  retry an authenticated dashboard login or a CURVE probe (as in ASPS-726) once the rate
  limit clears.

---

## ASPS-729 — Remove Backend sidecar — DONE (2026-08-23)

### Blocker #1 (fixed before this session, PR #32 / commit `4b2460e`): `AgentGatewayService` hardcoded `localhost` for ports 50001/50002

`ASPSBackend14_J/WebApi/Services/AgentGatewayService.cs:47-48`:
```csharp
_reqEndpoint = $"tcp://localhost:{Options.BackendReqPort}";
_subEndpoint = $"tcp://localhost:{Options.BackendPubPort}";
```
This is the transport for the `/ws/agent` WebSocket gateway (`AgentWebSocketMiddleware`,
`AgentConnection`, `AgentFrameParser`/`Builder` — ADR-004 / ASPS-718 / ASPS-720), which
bridges browser/other WebSocket clients to Backend's ROUTER (50001) and PUB (50002)
sockets. **The host is not configurable** — no `AgentGateway:BackendHost` (or similar)
setting exists; `Options.BackendReqPort`/`BackendPubPort` are configurable but the host
is a literal string constant.

Confirmed live in the current (post-ASPS-728) `ca-webapi-dev` logs:
```
AgentGatewayService started — REQ endpoint tcp://localhost:50001, SUB endpoint tcp://localhost:50002, enabled=True
```

**Why this doesn't break anything yet:** the Backend sidecar container is still running
inside the same pod as WebApi, sharing localhost — so `localhost:50001`/`:50002` still
correctly reaches Backend's sockets right now.

**Why this WILL break on Step 3 (ASPS-729):** once the Backend sidecar is removed from
`ca-webapi-dev`, nothing will be listening on `localhost:50001`/`:50002` inside the WebApi
container anymore. The ZMQ REQ/SUB sockets in `AgentGatewayService` would simply never
connect/receive — **no crash, no exception, no log error** (ZMQ connect is lazy/async) —
just a silent functional failure of the `/ws/agent` gateway for any client using it.

**Resolution (already done, prior session/PR):** commit `4b2460e` ("ASPS-725 Make
AgentGatewayService backend host configurable", merged via PR #32 / `9f834bb`) added
`AgentGatewayOptions.BackendHost` (default `"localhost"`, bound from config section
`AgentGateway`) and rebuilt `_reqEndpoint`/`_subEndpoint` from it:
```csharp
_reqEndpoint = $"tcp://{Options.BackendHost}:{Options.BackendReqPort}";
_subEndpoint = $"tcp://{Options.BackendHost}:{Options.BackendPubPort}";
```
Verified present in `ASPSBackend14_J/WebApi/Services/AgentGatewayService.cs:19,48-49` at the
start of this session.

### Blocker #2 (found and fixed this session): deployed WebApi image predated the fix

Set `AgentGateway__BackendHost=ca-backend-dev` on `ca-webapi-dev`'s `webapi` container via ARM
PATCH (see "Az CLI operational note" below) — PATCH succeeded, revision `--0000006` went
`Healthy`, but the startup log **still** showed
`AgentGatewayService started — REQ endpoint tcp://localhost:50001, SUB endpoint
tcp://localhost:50002`. Root cause: the running image
(`acraspsisaacdev.azurecr.io/asps-webapi:20260819-0604ba5`, built from commit `0604ba5`)
predates commit `4b2460e` — confirmed via
`git merge-base --is-ancestor 0604ba5 4b2460e` → true. The old binary has no `BackendHost`
property at all, so the env var was silently inert (no error — ASP.NET Core config binding
just ignores config keys nothing reads).

**Fix:** built and pushed a new WebApi image from current `main` (`9f834bb`, includes the
fix):
```bash
git archive HEAD | tar -x -C <scratch>/build-src   # clean tree, avoids locked .vs\*.vsidx files
az acr build --registry acraspsisaacdev \
  --image asps-webapi:20260823-9f834bb --image asps-webapi:latest \
  --file <scratch>/build-src/ASPSBackend14_J/WebApi/Dockerfile \
  <scratch>/build-src/ASPSBackend14_J
az containerapp update --name ca-webapi-dev --resource-group rg-asps-dev \
  --container-name webapi --image acraspsisaacdev.azurecr.io/asps-webapi:20260823-9f834bb
```
(`--image`-only update is safe — doesn't hit the CLI env-var-dropping bug — confirmed all 16
env vars including `AgentGateway__BackendHost` were intact after this update.)

New revision `--0000007`, `Healthy`/`RunningAtMaxScale`. Log now correctly shows:
```
AgentGatewayService started — REQ endpoint tcp://ca-backend-dev:50001, SUB endpoint tcp://ca-backend-dev:50002, enabled=True
```
No errors/exceptions in logs. `GET /` → 302 (Keycloak redirect), Keycloak OIDC discovery → 200.

**Why this is a real, generalizable finding (not just this task):** any Container App env-var
change is inert until the running image contains code that reads it — added as a new
troubleshooting entry (`docs/cloud/azure/troubleshooting.md`) since this will recur for any
future config-driven feature flag added to an already-deployed image.

### Sidecar removal

With `AgentGateway__BackendHost` verified working, removed the `backend` container from
`ca-webapi-dev` via ARM PATCH (`properties.template.containers` reduced to `[webapi]` only;
`properties.template.volumes` — the `curve-keys` Azure Files mount — kept, since
`CurveKeyManager` in client mode still reads `/keys/curve-server-public-key.txt` from it,
confirmed by reading `Business/Services/CurveKeyManager.cs` and by the post-removal log line
`CURVE server public key loaded for client use from /keys/curve-server-public-key.txt`).

New revision `ca-webapi-dev--0000008`:
- `properties.template.containers[].name` → `["webapi"]` (backend gone)
- `properties.template.containers[].resources` → `[{cpu:0.5, memory:"1Gi"}]` (Backend's
  1.0 CPU / 2 Gi freed automatically — no separate resize step needed)
- Revision health: `Healthy` / `RunningAtMaxScale`
- Logs: clean startup, `CQRS Client configured: tcp://ca-backend-dev:5556`,
  `AgentGatewayService started — REQ endpoint tcp://ca-backend-dev:50001, SUB endpoint
  tcp://ca-backend-dev:50002`, no errors/exceptions
- `GET https://ca-webapi-dev.../` → HTTP 302 (Keycloak redirect — app serving requests)
- Keycloak OIDC discovery → HTTP 200
- Standalone `ca-backend-dev--0000008` (unrelated revision numbering, separate app) still
  `Healthy` / `RunningAtMaxScale` throughout — unaffected by the WebApi-side change

**Not independently re-verified with an authenticated session** (same limitation as
ASPS-728 — no test-user credentials available in this session). Relying on clean logs, HTTP
checks, and health state as in ASPS-728.

**Rollback plan (not needed, but documented):** revision `ca-webapi-dev--0000005` (sidecar
still present) remains in Container Apps revision history and can be reactivated if a
regression is found later; `ca-backend-dev` was never touched by this change.

---

## ASPS-730 — Update CI/CD pipeline for separate Backend deploy — DONE (2026-08-23)

Updated `.github/workflows/deploy.yml`:

1. Added `BACKEND_APP: ca-backend-dev` to the top-level `env:` section.
2. Added `deploy-backend` job — `needs: [build-push-backend]`, plain
   `az containerapp update --image` against `ca-backend-dev` (same pattern as the existing
   `deploy-angular` job). No sidecar YAML patching.
3. Renamed the old `deploy` job to `deploy-webapi` and simplified it to a plain
   `az containerapp update --image` against `ca-webapi-dev` — removed the YAML
   export/`sed`-patch/re-apply steps entirely (no longer needed: `ca-webapi-dev` is
   single-container since ASPS-729).
4. Updated the `workflow_dispatch.inputs.deploy_backend` description from
   `"Deploy Backend (sidecar)"` to `"Deploy Backend"`.
5. Updated the section comment above the deploy jobs to describe the new standalone-apps
   model instead of the sidecar pattern.

**Verification:**
```bash
python -c "import yaml; d=yaml.safe_load(open('.github/workflows/deploy.yml')); print('YAML valid. Jobs:', list(d['jobs'].keys()))"
# -> YAML valid. Jobs: ['detect-changes', 'build-test', 'build-push-backend', 'build-push-webapi',
#    'build-push-angular', 'deploy-webapi', 'deploy-backend', 'deploy-angular']
```
Not run through an actual GitHub Actions execution this session (no code push triggering it
yet) — YAML syntax + job graph validated locally; each `deploy-*` job's steps mirror the
already-proven `deploy-angular` pattern exactly (same `az containerapp update --image` shape),
so no new untested pattern was introduced.

---

## Docs updated (per CLAUDE.md "Azure docs — update on every change")

- `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md` — Current Azure State table, new "Target
  Architecture — Standalone Backend + WebApi" section, AD-3 correction (FQDN vs app-name),
  new AD-9 (full rationale for the split), Steps 7+8/9/13 marked historical/superseded with
  pointers to the new Step 16, new Step 16 (this change), Ports Reference, Environment
  Variables Reference (split into `ca-backend-dev` / `ca-webapi-dev` sections)
- `docs/cloud/AZURE_ARCHITECTURE.md` — Architecture Overview diagram and prose, Container
  Apps section (`ca-webapi-dev` standalone, `ca-backend-dev` standalone+active, removed the
  old "deactivated" entry), Networking table, CI/CD pipeline diagram/mechanism, AD-9 entry
- `docs/cloud/ASPS_Azure_Architecture.html` — added deployment steps 15 (Angular, ASPS-724 —
  was previously missing) and 16 (this split), fixed port 50002 exposure badge
  (internal → ext tcp), updated connection labels to show app-name TCP addressing
- `docs/cloud/azure/troubleshooting.md` — two new entries: `az acr build` locked `.vs\*.vsidx`
  permission error (workaround: `git archive` into a clean scratch tree) and "env var change
  has no runtime effect" (image predates the code that reads the config key)
- `.claude/hats/devops/decisions.md` — no changes needed this session (the CLI-bug workaround
  entry from the prior session already covers everything used here)

---

## Az CLI operational note (durable — added to `.claude/hats/devops/decisions.md` too)

`az containerapp update --yaml`, and even the dedicated `--replace-env-vars` /
`--set-env-vars` flags, on this machine's `containerapp` extension (`1.3.0b4`, latest
available per `az extension update`) **silently drop every plain (non-`secretRef`) env
var value** on write — `secretRef`-backed entries survive, plain `value` entries come
back as `{"name": "X"}` with no `value` key at all. Reproduced twice (once via `--yaml`,
once via `--replace-env-vars`) on `ca-backend-dev`. **Workaround:** bypass the
`containerapp` CLI extension for anything beyond the already-proven-safe
"export-yaml → `sed` the image tag only → re-apply" CI/CD pattern (Step 13 of the
deployment guide) — use `az rest --method patch` directly against
`Microsoft.App/containerApps/{name}?api-version=2025-07-01` with the full
`properties.configuration` + `properties.template` body instead. Notes for that path:
- Raw ARM PATCH does **not** auto-preserve secret values the way the CLI does — you must
  fetch real values first (`az containerapp secret list --show-values`) and inline them
  in the PATCH body (kept in ephemeral scratch files only, never logged/committed/printed).
- The `show --output yaml`/`json` output includes several fields the PATCH schema at
  `api-version=2025-07-01` rejects as unknown: `targetPortHttpScheme` (under `ingress`),
  `revisionTransitionThreshold`, `targetLabel` (under `configuration`), `imageType`
  (under each container), `customMetricsSettings` (under `template`). Strip these before
  sending.
- Concurrent-modification guard: ARM returns `409 ContainerAppOperationInProgress` if a
  previous CLI operation (e.g. a still-provisioning revision from a prior buggy attempt)
  hasn't finished — poll `properties.provisioningState` to `Succeeded`/`Failed` first.

---

## Original ASPS-726 test (2026-08-22, earlier in the day)

---

## Goal

Determine whether the ZMQ/CURVE handshake succeeds between two Container Apps in the
same environment when connecting via **app name** (`ca-backend-dev:5556`), as opposed
to FQDN. The earlier documented failure (AD-3 / "Container Apps TCP ingress does NOT
forward ZMQ/CURVE") was only ever tested with FQDN addressing — app-name addressing had
never been tried.

## Result — CURVE HANDSHAKE SUCCEEDS via app-name addressing

Full ZMTP + CURVE handshake, encryption, and an authenticated round trip completed
successfully when connecting from inside `ca-webapi-dev`'s `backend` container to
`tcp://ca-backend-dev:5556` (app name, not FQDN), on the **first attempt**.

```
Connecting to tcp://ca-backend-dev:5556 with server key *}HCF.qRW-aIT#<?f@ub>3<$eZP!*(rj{@uhS2(<
Sent probe message, waiting for reply...
GOT REPLY (CURVE HANDSHAKE + ROUNDTRIP SUCCESS):
{"Success":false,"Message":"Invalid authenticated CQRS envelope."}
```

The `Success:false` / "Invalid authenticated CQRS envelope" message is **expected and
proves the handshake worked** — the probe script only performed the CURVE layer (using
the real server public key from the shared `/keys` volume) and did not additionally
apply `CqrsChannelSecurity` (HMAC) framing, so `NetMQCqrsTransport` correctly rejected
the payload at the application layer, *after* CURVE decryption succeeded. Getting any
structured JSON reply at all is only possible once the CURVE handshake, encryption,
and REQ/REP round trip all worked — a CURVE failure (or Envoy stripping the ZMTP
wire protocol) would have produced a silent timeout instead (see baseline below).

A raw TCP baseline connect (no ZMQ/CURVE) also succeeded first, confirming app-name
DNS resolution + plain TCP connect work independently of the CURVE layer:

```
TCP CONNECT OK: ('100.100.246.6', 5556)
```

### Conclusion for ASPS-725

App-name addressing (`<APP_NAME>:<PORT>`) between two Container Apps in the same
environment **does forward ZMQ/CURVE traffic correctly**, unlike FQDN-based ingress
(Envoy/HTTP-aware proxy path), which strips the ZMTP wire protocol. This unblocks
splitting Backend out of the `ca-webapi-dev` sidecar into its own Container App
(`ca-backend-dev`), using internal TCP ingress + app-name addressing from WebApi,
instead of localhost sidecar networking.

**Recommendation:** ASPS-725 (split Backend out of sidecar) can proceed using
app-name addressing over internal TCP ingress. Cloud Architect should design the
target topology (single active revision per app, scaling behavior, whether
`ca-backend-dev` keeps `additionalPortMappings` for 5555/50001/50002 or those also
move to app-name/internal ingress). DevOps then implements per that design.

---

## Environment used

| Resource | Value |
|---|---|
| Resource Group | `rg-asps-dev` |
| Container Apps Environment | `cae-asps-dev` |
| Region | North Europe |
| Existing app | `ca-backend-dev` (deactivated standalone Backend, revision `ca-backend-dev--0000005`) |
| Test client | `backend` sidecar container inside the **running** `ca-webapi-dev` app |

`ca-backend-dev` already had internal TCP ingress configured (pre-existing, unmodified):
```json
"ingress": {
  "external": false,
  "targetPort": 5556,
  "transport": "Tcp",
  "additionalPortMappings": [
    {"targetPort": 5555, "exposedPort": 5555, "external": false},
    {"targetPort": 50001, "exposedPort": 50001, "external": false},
    {"targetPort": 50002, "exposedPort": 50002, "external": false}
  ]
}
```
Image: `acraspsisaacdev.azurecr.io/asps-backend:0.2.0` (older tag than the sidecar's
`20260819-0604ba5` — not updated for this test since it wasn't needed to validate the
transport-layer question; CURVE/NetMQ transport code has not changed between these
tags in a way relevant to this test).

Both `ca-backend-dev` and `ca-webapi-dev` mount the same Azure Files share
(`curvekeys` → `/keys`), so `ca-backend-dev`'s Backend container loaded the
**same existing CURVE keypair** already in use by the sidecar (no new keypair was
generated — confirmed by the server public key returned during the test matching the
key already on the shared volume read from `/keys/curve-server-public-key.txt`).

## Commands run (chronological)

```bash
# 1. Confirm starting state (pre-existing, both unmodified by this test)
az containerapp show --name ca-backend-dev --resource-group rg-asps-dev
az containerapp show --name ca-webapi-dev --resource-group rg-asps-dev
az containerapp revision show --name ca-backend-dev --resource-group rg-asps-dev \
  --revision ca-backend-dev--0000005 --query "{active:properties.active,replicas:properties.replicas,runningState:properties.runningState}"
# -> active:false, replicas:0, runningState:Stopped (confirmed deactivated)

# 2. Reactivate ca-backend-dev (no config changes — just activate the existing revision)
az containerapp revision activate --name ca-backend-dev --resource-group rg-asps-dev \
  --revision ca-backend-dev--0000005
# Polled until healthState:Healthy, runningState:RunningAtMaxScale (~90s)

# 3. Exec into the ALREADY-RUNNING ca-webapi-dev "backend" sidecar container
#    (ca-webapi-dev itself was never touched/redeployed for this test)
az containerapp exec --name ca-webapi-dev --resource-group rg-asps-dev --container backend \
  --command "python3 -m pip install --break-system-packages --quiet pyzmq"

# 4. Baseline: raw TCP connect test via app name (no CURVE)
az containerapp exec --name ca-webapi-dev --resource-group rg-asps-dev --container backend \
  --command "python3 -c \"import socket; s=socket.create_connection(('ca-backend-dev', 5556), timeout=5); print('TCP CONNECT OK:', s.getpeername()); s.close()\""
# -> TCP CONNECT OK: ('100.100.246.6', 5556)

# 5. Read the existing shared CURVE server public key (from the shared /keys volume)
az containerapp exec --name ca-webapi-dev --resource-group rg-asps-dev --container backend \
  --command "cat /keys/curve-server-public-key.txt"
# -> *}HCF.qRW-aIT#<?f@ub>3<$eZP!*(rj{@uhS2(<

# 6. Write a small pyzmq probe script into the container (base64-transferred, ephemeral,
#    not persisted to the image) that performs a full CURVE client handshake against
#    tcp://ca-backend-dev:5556 using the real server public key, sends one probe frame,
#    and reads the reply with an 8s timeout.
az containerapp exec --name ca-webapi-dev --resource-group rg-asps-dev --container backend \
  --command "python3 -c \"import base64;open('/tmp/test_curve.py','w').write(base64.b64decode('<...>').decode())\""

# 7. Run the probe
az containerapp exec --name ca-webapi-dev --resource-group rg-asps-dev --container backend \
  --command "python3 /tmp/test_curve.py tcp://ca-backend-dev:5556"
# -> GOT REPLY (CURVE HANDSHAKE + ROUNDTRIP SUCCESS):
#    {"Success":false,"Message":"Invalid authenticated CQRS envelope."}

# 8. Cleanup — deactivate ca-backend-dev again (restore prior state)
az containerapp revision deactivate --name ca-backend-dev --resource-group rg-asps-dev \
  --revision ca-backend-dev--0000005
# Verified: active:false, replicas:0, runningState:Stopped

# 9. Verify ca-webapi-dev was never modified
az containerapp show --name ca-webapi-dev --resource-group rg-asps-dev \
  --query "properties.runningStatus"                                    # -> Running
az containerapp show --name ca-webapi-dev --resource-group rg-asps-dev \
  --query "properties.template.containers[?name=='backend'].image"      # -> asps-backend:20260819-0604ba5 (unchanged)
```

Note: hit an `az containerapp exec` rate limit (`429 Too Many Requests`, `retry-after: 600`)
once mid-session from repeated short exec calls; the very next retry (after the transient
condition cleared) succeeded immediately — not a real blocker, just be economical with
exec calls (batch commands per session rather than one exec call per line).

## Test script used (`/tmp/test_curve.py`, ephemeral — not committed, not persisted to any image)

```python
import zmq, sys
ctx = zmq.Context()
socket = ctx.socket(zmq.REQ)
client_public, client_secret = zmq.curve_keypair()
socket.curve_secretkey = client_secret
socket.curve_publickey = client_public
with open('/keys/curve-server-public-key.txt', 'r') as f:
    server_public_z85 = f.read().strip()
socket.curve_serverkey = server_public_z85.encode('ascii')
socket.setsockopt(zmq.RCVTIMEO, 8000)
socket.setsockopt(zmq.SNDTIMEO, 8000)
socket.setsockopt(zmq.LINGER, 0)
endpoint = sys.argv[1] if len(sys.argv) > 1 else 'tcp://ca-backend-dev:5556'
socket.connect(endpoint)
socket.send_string('{"$type":"Test.PingProbe","Test":"ASPS-726-curve-probe"}')
try:
    print('GOT REPLY:', socket.recv_string())
except zmq.error.Again:
    print('TIMEOUT - no reply within 8s')
```

Why this is a valid test (not app-logic — pure transport probe, no production code
touched): `NetMQCqrsTransport`/`ApplyServerCurve` (server side, in
`Business/Services/CurveKeyManager.cs` and `Business/Messaging/Transport/NetMQ/`)
applies CURVE with **no ZAP domain / client allow-list** — any client presenting a
valid CURVE handshake against the correct server public key is accepted at the
transport layer, and rejection only happens afterwards at the app layer (HMAC/
`CqrsChannelSecurity` envelope check inside `NetMQCqrsTransport`). This means a bare
CURVE probe (without reproducing the HMAC envelope) is a legitimate, sufficient way to
isolate "did CURVE work" from "was the message valid" — confirmed by reading
`ASPSBackend14_J/Business/Services/CurveKeyManager.cs` and
`ASPSBackend14_J/Business/Messaging/Transport/NetMQ/NetMQCqrsClient.cs` before writing
the probe.

## Files read (no application code changed)

- `docs/cloud/AZURE_DEPLOYMENT_GUIDE.md`
- `docs/cloud/AZURE_ARCHITECTURE.md`
- `ASPSBackend14_J/WebApi/Controllers/DashboardApiController.cs` (ruled out — requires
  `[Authorize(Roles="Admin")]`, not usable for an unauthenticated connectivity probe)
- `ASPSBackend14_J/WebApi/Pages/Index.cshtml.cs` (ruled out — same reason, all Razor
  Pages require auth via `AuthorizeFolder("/", "AdminPolicy")` in `Program.cs`)
- `ASPSBackend14_J/Business/Messaging/Transport/NetMQ/NetMQCqrsClient.cs`
- `ASPSBackend14_J/Business/Services/CurveKeyManager.cs`

## Not yet done (belongs to ASPS-725 proper, not this test)

- No infrastructure was permanently changed — `ca-backend-dev` was reactivated then
  deactivated again; `ca-webapi-dev` was never modified. This handoff is a **test
  result**, not a completed migration.
- Decision on final topology (does `ca-backend-dev` become the permanent standalone
  Backend app? do ports 5555/50001/50002 move off the sidecar too?) is a Cloud
  Architect design decision, then a DevOps implementation task — not started.
- `docs/cloud/AZURE_ARCHITECTURE.md` / `AZURE_DEPLOYMENT_GUIDE.md` still describe the
  **current** sidecar architecture as-is — no change needed there until ASPS-725 is
  actually implemented (this was a test, not a deployment change). When ASPS-725
  implementation happens, update AD-3 wording (currently says TCP ingress does NOT
  forward CURVE — needs qualification: "via FQDN; app-name addressing does work") and
  the sidecar diagram.

## Continuation point (superseded by ASPS-729/730 sections above — kept for history)

~~Next agent (Cloud Architect for design, then DevOps for implementation) should read
this handoff, then design the standalone-Backend topology using app-name internal TCP
addressing, then execute as a normal branch + QA-gated task per
`.claude/rules/task-workflow.md`.~~ — **Done**: see the "ASPS-729" and "ASPS-730" sections
above. Current continuation point below.

---

## Current continuation point (2026-08-23, end of ASPS-729/730 session)

**What's done:** ASPS-726, ASPS-727, ASPS-728, ASPS-729, ASPS-730 all complete and verified
live in Azure (`rg-asps-dev`). `ca-webapi-dev` is single-container (WebApi only), reads CURVE
public key from the shared `curve-keys` volume, talks to `ca-backend-dev` via app-name TCP for
both CQRS and the `/ws/agent` gateway. `ca-backend-dev` is the standalone, active Backend.
CI/CD (`deploy.yml`) deploys both independently via plain `--image` updates.

**Not yet done (next agent):**
1. **Commit + push** the branch `asps-725-remove-sidecar-update-cicd` (code change: none —
   this branch is docs + workflow only, since the actual `AgentGatewayService` fix already
   landed on `main` via PR #32 before this session; the Azure infra changes were applied
   live via `az`/ARM PATCH, not via code in this branch).
2. **Open a PR** to `main` for the doc/workflow changes once the branch is pushed, per
   `.claude/rules/task-workflow.md` (JIRA transition to In Review, id 31, after PR is open).
3. **QA gate** — this session's pre-QA checklist for a docs+YAML-only change:
   build/tests N/A (no application code changed in this branch), YAML validated locally,
   Azure state verified live (revisions healthy, logs clean, HTTP checks pass) — see
   evidence above. QA agent should independently confirm: (a) `ca-webapi-dev` /
   `ca-backend-dev` are healthy right now, (b) `deploy.yml` YAML is valid and the job graph
   matches what's described here, (c) docs accurately describe the live Azure state.
4. Optional cleanup (not urgent, not done this session): the old standalone-Backend
   `ca-backend-dev` revision `0000005` mentioned in the ASPS-726 test section above (image
   `asps-backend:0.2.0`) is long superseded by the current active revision — no action
   needed, just don't confuse it with the current `ca-backend-dev`.
5. `docs/cloud/ASPS_Azure_Architecture.html` was updated minimally (new steps 15/16, port
   50002 badge, connection labels) — it still has pre-existing drift unrelated to this task
   (e.g. WebApi shown as port `:5001` instead of the actual `:8080` ingress target port) that
   predates this session; flagged here per "no silent side-fixes" rather than changed, since
   it's outside ASPS-729/730 scope.
