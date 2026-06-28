---
name: netmq-endpoint
description: Add a new NetMQ endpoint to ASPSBackend with CURVE encryption applied correctly. Covers ROUTER/DEALER/PUB/SUB/PULL/PUSH socket selection, port allocation, key wiring, and the security debts to surface.
---

# /netmq-endpoint

Scaffolds a new NetMQ endpoint inside the Business layer with encryption applied via `CurveKeyManager`. Picks the right socket type for the user's traffic pattern and surfaces the existing security debts so they don't get repeated.

## When to invoke
- User wants to expose a new NetMQ socket from the backend.
- User says "add ZMQ endpoint", "new NetMQ port", "expose <service> over NetMQ".

## Ask first

Before writing code, confirm:

1. **Traffic pattern** — drives socket choice:
   | Need | Server socket | Client socket |
   |---|---|---|
   | Client → server request/reply, concurrent clients | `RouterSocket` | `DealerSocket` (or `RequestSocket`) |
   | Server broadcasts to many clients | `PublisherSocket` | `SubscriberSocket` |
   | Fire-and-forget upload to server | `PullSocket` | `PushSocket` |

   If the user says "I want a request/reply" without specifying concurrency, default to ROUTER on the server (matches `RealTimeAlertListener` at port 50001) — never `ResponseSocket`, which serializes clients.

2. **Port** — pick an unused port and verify against:
   - `appsettings.json` (look at existing `Ports` section)
   - `CLAUDE.md` "Ports / messaging" table
   - `docker-compose*.yml` if the deployment exposes it

   Currently allocated: 50001 (alert listener, ROUTER+CURVE), 50002 (notification PUB+CURVE), 5555 (business endpoint), 5556 (CQRS gateway).

3. **Encryption** — strongly default to CURVE. CLAUDE.md flags ports 5555 / 5556 as "security debt" because they bind without it; do **not** add a new endpoint without CURVE unless the user explicitly accepts that debt and the reason is documented.

4. **Bind address** — strongly default to `tcp://127.0.0.1:<port>` for new endpoints. Existing endpoints use `tcp://*:<port>` (bind-all) which is also part of the security debt. Surface the choice to the user.

## Reference implementation

The canonical example is `Business/Messaging/RealTimeAlertListener.cs` (port 50001, ROUTER + CURVE). Mirror its structure for new server-side endpoints.

### Server socket + CURVE

```csharp
_routerSocket = new RouterSocket();
_routerSocket.Options.Linger = TimeSpan.Zero;
_curveKeyManager?.ApplyServerCurve(_routerSocket);   // applies if IsEnabled
_routerSocket.Bind($"tcp://127.0.0.1:{_port}");
```

`CurveKeyManager` is injected via DI (`Business/Services/CurveKeyManager.cs`); it reads server keys from `appsettings.json` and silently no-ops if encryption is disabled. Always call `ApplyServerCurve` **before** `Bind` — applying after has no effect on already-bound sockets.

### Client socket + CURVE

Use `ApplyClientCurve` (sets the local key pair + the remote server's public key). Apply **before** `Connect`.

### Polling loop

Run socket I/O off a `Task.Run` worker; expose `Start()` / `Stop()` on the service. Set `_isRunning` flag for cooperative shutdown. Always `Linger = TimeSpan.Zero` so process exit isn't blocked.

## Files to create / modify

1. **`Business/Messaging/<Name>Service.cs`** — the service class. Pattern: ctor receives `ILogger`, `CurveKeyManager`, `IServiceProvider`, port from config. Implements `Start()`, `Stop()`, the listen loop.

2. **`Business/Services/BusinessServiceRegistration.cs`** — register as `AddSingleton` + `AddHostedService` if it should auto-start (same pattern as `SimulationRunner`).

3. **`ASPSBackend/appsettings.json`** — add the new port under the existing `Ports` section. Add CURVE keys to the relevant section if a new server identity is needed (almost always reuse the existing one).

4. **`docker-compose*.yml`** — expose only if a client outside the host needs it. Default: do not expose.

5. **`CLAUDE.md`** — update the "Ports / messaging" table.

## Verification

1. Build clean (`MSB3027/MSB3021` = file lock, OK; real failures are `CS####`).
2. Start ASPSBackend → log line should read:
   ```
   <Name> started on tcp://127.0.0.1:<port> (<Mode> mode, CURVE encrypted)
   ```
3. A quick smoke test from the Python desktop agent or a small NetMQ test client — confirm message round-trip with CURVE enabled. If encryption is misconfigured, the handshake fails silently on the client; surface this to the user as a known failure mode.

## Never

- Bind a new endpoint with `tcp://*:<port>` without explicit user approval — the existing two ports doing this (5555, 5556) are documented security debt.
- Skip CURVE without documenting the reason.
- Apply CURVE *after* `Bind` / `Connect` — the call is silently ineffective and the socket runs unencrypted.
- Reuse a port already listed in `CLAUDE.md`.
- Block process exit on socket shutdown — always set `Linger = TimeSpan.Zero`.

## Output convention

```
Endpoint: <Name>Service
Socket: <ROUTER|PUB|PULL|...>
Bind: tcp://<addr>:<port>
CURVE: enabled/disabled (<reason if disabled>)
Files created:
  - Business/Messaging/<Name>Service.cs
  - (modified) Business/Services/BusinessServiceRegistration.cs
  - (modified) ASPSBackend/appsettings.json
  - (modified) CLAUDE.md (ports table)
Smoke test: PASS/FAIL <details>
```
