---
name: docker-deps-from-lockfile
description: Dockerfile must install Python deps from requirements.lock.txt, never cherry-pick individual packages — missing deps cause silent analyzer failures
metadata:
  type: feedback
---

Install Python dependencies in Docker from the project's `requirements.lock.txt`, never by listing individual packages in the Dockerfile.

**Why:** The Backend Dockerfile originally installed only `playwright scikit-learn requests`, but the basic-url-analyzer needs ~30 packages. The missing deps caused the Python subprocess to fail on import, but the Backend swallowed the error in its background dispatch — resulting in silent analysis failures where only cached/whitelisted results worked.

**How to apply:** When a Dockerfile installs Python packages for any analyzer or Python component, always use `pip install -r requirements.lock.txt` (with `--no-deps` for lock files). Copy the Analyzers directory BEFORE the pip install so the lock file is available. If a new dependency is added to the analyzer, the lock file update flows automatically to the next Docker build.
