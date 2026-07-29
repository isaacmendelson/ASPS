"""
Remote Monitor — CLI Wrappers for Remote Access Tools

Contains:
  - AnyDeskCLI    : thin wrapper around AnyDesk.exe CLI
  - TeamViewerCLI : thin wrapper for TeamViewer.exe CLI with kill fallback

Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

import re
import shutil
import subprocess
import logging
from pathlib import Path
from typing import Optional

from remote_monitor_config import MonitorConfig

logger = logging.getLogger(__name__)


# ══════════════════════════════════════════════════════════════════════════════
# ANYDESK CLI — wrapper for AnyDesk.exe command-line interface
# ══════════════════════════════════════════════════════════════════════════════

class AnyDeskCLI:
    """
    Thin wrapper around the `AnyDesk.exe` CLI. Used to:
      - read the local AnyDesk ID (`--get-id`)
      - read AnyDesk status (`--get-status`)
      - disconnect an active session (`--disconnect`)

    The disconnect path is the bridge between a backend ProtectiveAction
    `BlockRemoteAccess` and a real local effect on the user's machine.

    All methods return None / False on Windows-only failures (e.g., AnyDesk
    not installed). Safe to instantiate even if AnyDesk is absent.
    """

    def __init__(self):
        self.exe: Optional[Path] = self._find_exe()

    def _find_exe(self) -> Optional[Path]:
        """Auto-detect AnyDesk.exe location (PATH first, then standard paths)."""
        try:
            on_path = shutil.which("AnyDesk")
            if on_path:
                return Path(on_path)
        except Exception:
            pass
        for candidate in MonitorConfig.ANYDESK_EXE_CANDIDATES:
            try:
                if candidate.exists():
                    return candidate
            except (OSError, PermissionError):
                continue
        return None

    def is_available(self) -> bool:
        return self.exe is not None and self.exe.exists()

    def _run(self, *args: str, timeout: float = 5.0) -> Optional[str]:
        if not self.is_available():
            return None
        try:
            result = subprocess.run(
                [str(self.exe), *args],
                capture_output=True,
                text=True,
                timeout=timeout,
            )
            return (result.stdout + result.stderr).strip()
        except (subprocess.TimeoutExpired, FileNotFoundError, OSError) as e:
            logger.debug(f"AnyDeskCLI '{' '.join(args)}' failed: {e}")
            return None

    def get_id(self) -> Optional[str]:
        """Return the local AnyDesk ID (numeric, 5-12 digits) or None."""
        out = self._run("--get-id")
        if not out:
            return None
        m = re.search(r"\d{5,12}", out)
        return m.group(0) if m else None

    def get_status(self) -> Optional[str]:
        """Return current AnyDesk status string (e.g., 'online', 'busy')."""
        return self._run("--get-status")

    def disconnect(self) -> bool:
        """Disconnect the active session. Returns True if the command succeeded."""
        if not self.is_available():
            logger.warning("AnyDeskCLI.disconnect: AnyDesk.exe not found")
            return False
        out = self._run("--disconnect")
        ok = out is not None
        logger.info(f"AnyDeskCLI.disconnect -> {'sent' if ok else 'failed'}")
        return ok


# ══════════════════════════════════════════════════════════════════════════════
# TEAMVIEWER CLI — wrapper for TeamViewer.exe command-line interface
# ══════════════════════════════════════════════════════════════════════════════

class TeamViewerCLI:
    """
    Thin wrapper for disconnecting an active TeamViewer session.

    Strategy (in order):
      1. `TeamViewer.exe --action disconnect`  — supported since TV 13+.
         Returns a result dict {"success": True/False, "reason": str}.
      2. Process-kill fallback: terminate `teamviewer.exe` via taskkill when
         the CLI flag is unavailable or returns non-zero.  This is destructive
         (closes the whole app), but it guarantees the session ends.

    Safe to instantiate even when TeamViewer is not installed.
    """

    # Standard installation paths on Windows
    _EXE_CANDIDATES = [
        Path(r"C:\Program Files\TeamViewer\TeamViewer.exe"),
        Path(r"C:\Program Files (x86)\TeamViewer\TeamViewer.exe"),
    ]
    # Process names used for the kill fallback
    _PROCESS_NAMES = ["teamviewer.exe", "teamviewerservice.exe"]

    def __init__(self):
        self.exe: Optional[Path] = self._find_exe()

    def _find_exe(self) -> Optional[Path]:
        try:
            on_path = shutil.which("TeamViewer")
            if on_path:
                return Path(on_path)
        except Exception:
            pass
        for candidate in self._EXE_CANDIDATES:
            try:
                if candidate.exists():
                    return candidate
            except (OSError, PermissionError):
                continue
        return None

    def is_available(self) -> bool:
        return self.exe is not None and self.exe.exists()

    def _run_cli(self, *args: str, timeout: float = 5.0) -> Optional[int]:
        """Run TeamViewer.exe with given args.  Returns returncode or None."""
        if not self.is_available():
            return None
        try:
            result = subprocess.run(
                [str(self.exe), *args],
                capture_output=True,
                text=True,
                timeout=timeout,
            )
            logger.debug(
                f"TeamViewerCLI '{' '.join(args)}' -> rc={result.returncode} "
                f"stdout={result.stdout.strip()!r} stderr={result.stderr.strip()!r}"
            )
            return result.returncode
        except (subprocess.TimeoutExpired, FileNotFoundError, OSError) as e:
            logger.debug(f"TeamViewerCLI '{' '.join(args)}' failed: {e}")
            return None

    def _kill_process(self) -> bool:
        """Kill TeamViewer process(es) via taskkill.  Returns True on success."""
        killed_any = False
        for proc in self._PROCESS_NAMES:
            try:
                result = subprocess.run(
                    ["taskkill", "/F", "/IM", proc],
                    capture_output=True,
                    text=True,
                    timeout=5.0,
                )
                if result.returncode == 0:
                    logger.info(f"TeamViewerCLI._kill_process: killed {proc}")
                    killed_any = True
            except Exception as e:
                logger.debug(f"TeamViewerCLI._kill_process {proc}: {e}")
        return killed_any

    def disconnect(self) -> dict:
        """
        Disconnect the active TeamViewer session.

        Returns {"success": bool, "reason": str}.
        Possible reasons:
          "disconnected"       — CLI flag succeeded
          "killed_process"     — CLI unavailable/failed; process was killed
          "cli_not_found"      — TeamViewer.exe not found and no process to kill
          "disconnect_failed"  — CLI returned non-zero and process kill also failed
        """
        if self.is_available():
            rc = self._run_cli("--action", "disconnect")
            if rc == 0:
                logger.info("TeamViewerCLI.disconnect -> CLI flag succeeded")
                return {"success": True, "reason": "disconnected"}
            logger.info(
                f"TeamViewerCLI.disconnect -> CLI flag rc={rc}; trying process kill"
            )
        else:
            logger.warning("TeamViewerCLI.disconnect: TeamViewer.exe not found; trying process kill")

        # Fallback: kill the process
        if self._kill_process():
            return {"success": True, "reason": "killed_process"}

        if not self.is_available():
            return {"success": False, "reason": "cli_not_found"}
        return {"success": False, "reason": "disconnect_failed"}
