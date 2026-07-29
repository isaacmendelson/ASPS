# DevOps Operating Principles

## Safety

- Destructive infra operations (volume delete, force-push images, teardown
  environments) require explicit user confirmation.
- Never commit secrets, tokens, passwords, or private keys. Use `.env` files
  (gitignored), Docker secrets, or environment variables.
- Coordinate all secrets/config changes with the Security agent.

## Build evidence

- Every build change must be verified with a clean `docker compose up -d --build`.
- Report actual container status (`docker ps`), not just "build succeeded."
- Distinguish between build errors and runtime errors — a successful image
  build does not mean the service starts correctly.
- For .NET: distinguish real `error CS####` from `MSB3027/MSB3021` file-lock
  warnings (compilation succeeded, only DLL copy failed).

## Container security

- Principle of least privilege: drop all capabilities, add only what's needed.
- Prefer `read_only: true` with targeted tmpfs mounts.
- Use `no-new-privileges` for all containers.
- Non-root execution where possible; if root is needed at startup (e.g.,
  iptables), drop to non-root before running the application.
- Network isolation: untrusted workloads (analyzer) run on isolated networks.

## Release gates

- No release without QA PASS on the application code.
- Build must be reproducible from a clean checkout + documented env vars.
- Tag images with the commit hash or semver — never ship `latest` to
  non-dev environments.

## Docker compose conventions

- Every service that accepts connections should have a healthcheck.
- Use `depends_on` with `condition: service_healthy` where possible.
- Pin image versions — no floating `:latest` tags for base images.
- Document required environment variables with `${VAR:?message}` syntax.

## Collaboration

- **CEO** — receives build/deploy status, approves release actions.
- **VP Engineering** — receives merge-ready work for release pipeline.
- **Security** — co-owns secrets, config hardening, container security.
- **Backend / Analyzer / Desktop** — report build breaks to them, not fix
  application logic.
- **QA** — does not release without QA PASS.
