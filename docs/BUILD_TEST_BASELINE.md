# Reproducible Build and Test Baseline

ASPS-626 defines one supported Windows verification path for the .NET backend,
Python analyzer, Python desktop agent, and Chrome extension.

## Supported toolchain

| Tool | Exact version |
|---|---:|
| .NET SDK | 9.0.308 |
| .NET runtime / ASP.NET runtime | 8.0.29 |
| Python | 3.11.9 |
| Node.js | 22.23.2 |
| npm | 10.9.8 |

The intended exact versions are enforced by both `.python-version` files, the
extension `package.json`/`.nvmrc`, Docker image tags, and CI setup actions.

Note on .NET SDK vs runtime: the SDK version for host builds and CI is 9.0.308
(pinned in `global.json`). The .NET runtime inside containers is 8.0.29, which
matches the `net8.0` target framework. Docker build-stage images use
`mcr.microsoft.com/dotnet/sdk:8.0.423` and do **not** inherit `global.json` from
the host — global.json is intentionally not copied into Docker build contexts.

## Clean-machine verification

Install the supported toolchain, clone the repository, then run from the
repository root:

```powershell
pwsh ./scripts/verify.ps1 -Bootstrap
```

`-Bootstrap` creates component-local Python virtual environments, installs only
the committed Python locks, runs `pip check`, performs `npm ci`, restores NuGet
in locked mode, builds the solution, runs every deterministic test suite, and
validates the resulting failure set.

Generated reports are written to `artifacts/asps626/`, which is gitignored.

If your local Node.js/npm or Python versions differ from the pinned CI versions
but you have confirmed the builds are functionally equivalent, add
`-AllowRuntimeMismatch` to suppress version-check failures during local
development. CI always runs the exact pinned versions without this flag.

## Dependency locks

- NuGet uses `RestorePackagesWithLockFile` and one `packages.lock.json` per
  project. Restore uses `--locked-mode`.
- Analyzer and Desktop install exact transitive versions from
  `requirements.lock.txt` with `--no-deps`; `pip check` verifies consistency.
- Extension uses the committed `package-lock.json` through `npm ci`.
- `legacy-peer-deps=true` is intentional: `jest-chrome@0.8.0` declares support
  only through Jest 27, while the existing suite uses Jest 29. The clean install
  and full suite are still enforced.

## Test exception policy

All deterministic tests run. Known failures are not skipped or converted to
passes. `scripts/test-baseline-exceptions.json` records each temporary exception
with:

- exact component and selector;
- exact failure count and SHA-256 of the failing test IDs;
- responsible owner and reason;
- mandatory expiry date.

`scripts/check_test_baseline.py` fails for a new failure, a changed known failure
set, an unexpectedly fixed exception that was not removed, or an expired
exception. This prevents unrelated regressions from hiding inside a broad
quarantine.

Current exact exception baseline (as of 2026-07-30):

| Component | Passing | Known failing | Skipped/xfail |
|---|---:|---:|---:|
| .NET | 1,470 | 0 | 7 |
| Analyzer | 343 | 0 | 5 |
| Desktop | 245 | 0 | 2 |
| Extension | 221 | 74 | 0 |

The .NET and Analyzer exceptions recorded in the previous baseline have been
remediated by their respective owners and are no longer failing. The exception
manifest now has zero dotnet and analyzer entries.

The Analyzer live-site suite (`tests/test_real_sites.py`) is the only excluded
suite. It depends on uncontrolled external network/content, and its exclusion is
owner- and expiry-controlled in the same manifest.

## CI and containers

`.github/workflows/reproducible-baseline.yml` runs the same verifier on
`windows-2025` with SDK 9.0.308. Dockerfiles use exact .NET SDK 8.0.423 container
images (which are independent of the host `global.json`) and locked
restores/installs.
