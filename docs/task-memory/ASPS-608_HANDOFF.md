# ASPS-608 Handoff

- Task: `ASPS-608`
- Title: `[CODE REVIEW] Secure and authenticate the externally exposed CQRS gateway`
- Agent label: `backend`
- Status: QA PASS; ready for root commit and Jira closure

## Implementation

- CQRS gateway defaults to `tcp://127.0.0.1:5556`; Docker keeps `tcp://*:5556`
  only on the private Compose network and no longer publishes port 5556 to the host.
- Gateway and WebApi client require CURVE; there is no plaintext fallback.
- Requests use a versioned HMAC-SHA256 envelope containing client ID, timestamp,
  random nonce and payload. The server validates the client allowlist, clock window,
  signature in constant time, and rejects replayed nonces.
- Commands are denied unless their exact command type is in the configured allowlist.
- CQRS deserialization uses `TypeNameHandling.None`; gateway routing remains an
  explicit query/command type allowlist.
- WebApi CURVE client-only mode reads only the Backend public key. Docker stores
  public and private key material in separate volumes.
- `CQRSGateway.Start()` now requires a valid 32-byte CURVE public/private server
  keypair before binding. Client-only/public-key-only managers cannot act as a
  server, and `ApplyServerCurve` throws instead of silently returning.
- Real NetMQ integration tests connect to a running CURVE gateway and verify
  rejection of plaintext clients, unsigned envelopes, tampered payloads, replayed
  nonces, unauthorized client IDs, and unauthorized command types.

## Deployment/configuration

1. Generate a random secret of at least 32 UTF-8 bytes.
2. Set the same secret as `CQRS__SharedSecret` for Backend and WebApi. Compose
   requires it through the `CQRS_SHARED_SECRET` environment variable.
3. Backend writes its CURVE public key to the public-key volume. WebApi mounts
   that volume read-only and starts in `Security__CurveClientOnly=true` mode.
4. For non-Docker deployments, configure Backend `CQRS:BindEndpoint`,
   `CQRS:SharedSecret`, `CQRS:AllowedClientIds`, `CQRS:AllowedCommands`, and point
   WebApi `Security:ServerPublicKeyFilePath` at the Backend public-key file.
5. This is a deliberate protocol cutover: legacy unsigned/plaintext CQRS clients
   are rejected and must be upgraded with the WebApi deployment.

## Verification

- `dotnet build Business/Business.csproj -c Debug --nologo --no-restore -v:minimal`
  — succeeded, 0 compiler errors.
- WebApi and Backend builds with isolated `BaseOutputPath` — succeeded, 0 compiler
  errors. The normal solution output is locked by the running Backend/Visual Studio
  process (`MSB3027`/`MSB3021`), which is a copy failure rather than compilation.
- Initial pre-QA relevant tests: 25 passed, 0 failed, 0 skipped.
- QA returned FAIL with two Major findings: incomplete server-key fail-closed
  validation and missing live NetMQ negative integration coverage.
- Post-fix focused gateway tests: 11 passed, 0 failed, 0 skipped.
- Final post-fix relevant tests: 32 passed, 0 failed, 0 skipped.
- Final isolated-output Backend build: 0 errors.
- Final isolated-output WebApi build: 0 errors.
- Independent QA re-review: PASS. QA independently reproduced 11/11 focused
  gateway tests and 32/32 relevant CQRS/CURVE tests, plus successful Backend and
  WebApi builds. Docker runtime validation was unavailable; Compose security
  configuration was reviewed statically.

## Continuation

Root owns the isolated ASPS-608 commit, recording the commit hash and QA evidence
in Jira, and transitioning the issue to Done.
