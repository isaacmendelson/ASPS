---
name: user-python-interpreter
description: "On Isaac's Windows machine `python` and `python3` are different interpreters — `python` has the project dependencies, `python3` does not. Always use `python`."
metadata: 
  node_type: memory
  type: user
  originSessionId: 79421ab0-5013-4f83-811d-ef1680a5ec72
---

On Isaac's Windows machine, **`python` and `python3` are not the same interpreter.**

- `python` — the real installation with the ASPS desktop agent's dependencies (`python-dotenv`, `pyzmq`, `websockets`, …) installed.
- `python3` — resolves to a different interpreter (most likely the Microsoft Store stub at `C:\Users\Isaac\AppData\Local\Microsoft\WindowsApps\python3.exe`). Does not have the dependencies.

**How to apply:** When running anything for the ASPS desktop agent (`apps/desktop/win/src/main.py`, build scripts, diagnostics), always tell Isaac to use `python`, never `python3`. When suggesting fresh venv setup, also use `python -m venv .venv` (not `python3 -m venv .venv`).

**Reproduced twice in one session (2026-06-16):**
1. `python3 .\main.py` → `ModuleNotFoundError: No module named 'dotenv'` while `python .\main.py` worked.
2. Same pattern can affect any agent / analyzer / Python tooling in this repo.

If Isaac wants to eliminate the confusion long-term: Windows Settings → Apps → App execution aliases → disable `python3.exe` and `python.exe` aliases. (He hasn't done this — keep the recommendation in mind but don't push.)
