"""
Remote Monitor — Configuration

Central configuration constants and path definitions for the remote access
monitor. Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

import os
import sys
from pathlib import Path

IS_WINDOWS = sys.platform == "win32"


class MonitorConfig:
    """Central configuration for monitoring."""

    APPDATA = os.environ.get("APPDATA", "C:\\Users\\Default\\AppData\\Roaming")
    PROGRAMDATA = os.environ.get("PROGRAMDATA", "C:\\ProgramData")

    # AnyDesk paths
    SVC_TRACE = Path(PROGRAMDATA) / "AnyDesk" / "ad_svc.trace"
    UI_TRACE = Path(APPDATA) / "AnyDesk" / "ad.trace"
    CONN_TRACE = Path(APPDATA) / "AnyDesk" / "connection_trace.txt"

    # AnyDesk executable — auto-detected; first existing path wins
    ANYDESK_EXE_CANDIDATES = [
        Path(r"C:\Program Files (x86)\AnyDesk\AnyDesk.exe"),
        Path(r"C:\Program Files\AnyDesk\AnyDesk.exe"),
    ]

    # TeamViewer paths
    TV_CONNECTIONS_IN = Path(APPDATA) / "TeamViewer" / "Connections_incoming.txt"
    TV_CONNECTIONS_OUT = Path(APPDATA) / "TeamViewer" / "Connections.txt"

    # Monitoring settings
    POLL_INTERVAL = 1.0
    PROCESS_CHECK = 5.0

    # Ports
    ANYDESK_PORTS = {7070, 7071, 443, 80}
    TV_PORTS = {5938, 443, 80}
    VNC_PORTS = {5900, 5901, 5902, 5903}
    INFRASTRUCTURE_PORTS = [80, 443, 6568, 7070]
