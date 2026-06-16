---
name: velopack-publish
description: Build and publish a Velopack-installable release of the Windows desktop agent. Wraps PyInstaller --onedir + vpk pack with the constraints from the SCRUM-863 architecture decision.
---

# /velopack-publish

Produces a Velopack release (`.nupkg` + `Setup.exe`) that the auto-update code path expects. This is the **build-pipeline rework** still outstanding for SCRUM-863 — current `build_release.py` uses `--onefile` + Inno Setup, which is incompatible with Velopack apply.

## When to invoke
- User wants to cut a new desktop agent release for Velopack delivery.
- User says "publish agent", "velopack release", "vpk pack", "build agent .nupkg".

## Constraints — hard rules from SCRUM-863 architecture

These come from the live test that passed on 2026-05-23. Violating them breaks the apply path.

1. **PyInstaller must use `--onedir`, NOT `--onefile`.** Velopack's `Update.exe apply` swaps a *directory tree* (`current\`) — a single .exe gives it nothing to swap.

2. **Version must be 3-part SemVer.** `vpk pack --packVersion` rejects 4-part versions like `0.1.2.0`. Use `0.1.2`. Confirm `src/version.py` is 3-part before packing.

3. **First install must go through `Setup.exe`.** Velopack's `Update.exe apply` throws `RuntimeError: Could not auto-locate app manifest` unless the running app was originally installed via `Setup.exe`. A dev / source run can build and download updates but cannot self-apply.

4. **Velopack's Python `UpdateManager` is NOT used.** The agent's [agent_updater.py](c:/Jobs/ASPS/GitHub/Software/apps/desktop/win/src/services/agent_updater.py) shells out to `Update.exe apply --package <our.nupkg> --waitPid <pid> --restart`. No feed, no network — package is downloaded + SHA-256-verified by the agent itself via the backend control plane (SCRUM-863 Phases 1–7).

## Steps

### 1. Pre-flight

```bash
cd apps/desktop/win
```

- Check `src/version.py` — confirm 3-part (e.g. `VERSION = "0.1.3"`).
- Check `vpk` is installed: `vpk --version`. If missing: `dotnet tool install -g vpk`.
- Working tree is clean (`git status`) — release should match a commit.

### 2. PyInstaller `--onedir`

The current `build_release.py` is `--onefile` based — **do not just run it**. The recipe (proven in the 2026-05-23 spike) is:

```bash
python -m PyInstaller src/main.py \
  --name AntiScamAgent \
  --onedir \
  --noconsole \
  --windowed \
  --icon resources/app.ico \
  --add-data "src;src" \
  --hidden-import <as needed> \
  --distpath dist \
  --workpath build \
  --noconfirm
```

Output: `dist/AntiScamAgent/` with `AntiScamAgent.exe` plus all DLLs and the `_internal/` folder.

If extending `build_release.py`, gate on `--velopack` so the existing onefile/Inno path stays available until the rework lands.

### 3. Verify the onedir runs

Before packing, smoke-test:

```bash
dist/AntiScamAgent/AntiScamAgent.exe --version
```

Should print the 3-part version. If it crashes here, `vpk pack` is wasted effort.

### 4. `vpk pack`

```bash
vpk pack \
  --packId AntiScamAgent \
  --packVersion <3-part version from src/version.py> \
  --packDir dist/AntiScamAgent \
  --mainExe AntiScamAgent.exe \
  --packAuthors "ASPS" \
  --packTitle "AntiScam Desktop Agent"
```

Output: a `Releases/` directory with:
- `AntiScamAgent-<version>-full.nupkg` — the update package the agent downloads
- `Setup.exe` — the installer for new machines
- `RELEASES` — the Velopack catalog file

### 5. Verify the package layout

```bash
unzip -l Releases/AntiScamAgent-<version>-full.nupkg | grep -E "current/|Update.exe"
```

You should see `current/AntiScamAgent.exe`, `current/_internal/...`, and `Update.exe` at the package root. If `current/` is missing or empty, the `--packDir` argument was wrong.

### 6. Upload to the backend's release storage

Where the backend serves packages from is environment-specific — confirm with the user. The agent's download URL is constructed by the backend's `VersionUpdateAvailable` push (SCRUM-863 Phases 2–4); the file just needs to be where that URL points.

After upload, the agent's SHA-256 verify will fail if the file changed between upload and download — make sure the backend records the hash of the file actually served.

### 7. Live-test before announcing

Per SCRUM-863's open items: the crash-loop rollback path (`agent_updater._rollback`) was coded but NOT live-verified (step 7 of the live test was skipped). If this release exercises a new code path in the agent, add a manual test of the rollback before announcing.

## Never

- Pack with `--onefile` PyInstaller output. The apply will succeed but the running agent breaks because there's no directory to swap.
- Use a 4-part version. `vpk pack` rejects it; if you bypass it with a 3-part version that doesn't match `src/version.py`, the agent's version comparison breaks.
- Skip the smoke test step 3. Packing a broken onedir wastes everyone's time.
- Distribute the `.nupkg` to a fresh machine. Fresh machines need `Setup.exe`; the .nupkg is for updates only.
- Hand-edit the `Releases/RELEASES` catalog. Velopack regenerates it.

## Output convention

```
Version: <3-part>
Build mode: onedir (Velopack)
PyInstaller: PASS/FAIL
Smoke test (--version): PASS/FAIL
vpk pack: PASS/FAIL
Outputs:
  - Releases/AntiScamAgent-<version>-full.nupkg (<size>)
  - Releases/Setup.exe (<size>)
  - Releases/RELEASES
Next: upload + record SHA-256 + live-test rollback (SCRUM-863 step 7)
```
