# Workflow: Release

Package, version, and ship verified work safely and repeatably.

> NOTE: ASPS release tooling is still maturing — treat steps marked TODO as targets to build.

## Trigger
A set of merged, QA-passed changes deemed ready to ship (decided by CEO + VP Engineering).

## Roles Involved
CEO (go/no-go) · VP Engineering (readiness) · DevOps (lead — build/release) ·
Security (release hardening, secrets) · QA (release verification) · Knowledge Manager (release notes/ADRs).

## Stages
1. **Cut** — VP Eng confirms scope; DevOps determines the version bump.
2. **Build** — DevOps produces reproducible artifacts (dotnet, desktop agent packaging, extension bundle).
3. **Harden** — Security checks secrets/config and release surface.
4. **Verify** — QA smoke-tests the built artifacts (not just source).
5. **Ship** — DevOps deploys/publishes per the runbook. *(TODO: define channels/pipeline.)*
6. **Record** — Knowledge Manager files release notes; ADR for any release-process decision.

## Hand-offs
- VP Eng → DevOps: release scope + version intent.
- DevOps → QA: built artifacts to verify.
- DevOps → Knowledge Manager: release notes content.

## Gates
- **CEO go/no-go** before ship.
- QA PASS on the **built artifact**; Security sign-off on secrets/config.

## Definition of Done
- [ ] Versioned, reproducible artifact built from a clean checkout.
- [ ] Security check clean (no secrets shipped); QA verified the artifact.
- [ ] Shipped per runbook; release recorded.
- [ ] Rollback path known.
