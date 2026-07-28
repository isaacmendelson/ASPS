"""
AntiScam Desktop App - Configuration
"""

import os
from dotenv import load_dotenv
from version import VERSION

# Load environment variables from .env file
load_dotenv()


def _parse_bool(value: str) -> bool:
    """Parse string to boolean."""
    return value.lower() in ('true', '1', 'yes', 'on')

# Debug Mode - defaults to False for production safety
DEBUG_MODE = _parse_bool(os.environ.get('DEBUG_MODE', 'false'))

# WebSocket Ports for Extension Communication (local)
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]

# Backend Server - ZeroMQ
BACKEND_HOST = "127.0.0.1"  # Local testing
#BACKEND_HOST = "app.asps.io"  # AWS Production
#BACKEND_HOST = "100.88.78.75"  # Old local server
BACKEND_REQ_PORT = 50001  # Request/Response (REQ/REP pattern)
BACKEND_SUB_PORT = 50002  # Notifications (PUB/SUB pattern)

# CURVE Encryption - Server public key (Z85 encoded)
# The backend generates its keypair on first run and logs the public key.
# Set this value via environment variable or a local config file — never hardcode in source.
#
# Priority:
#   1. Environment variable: ANTISCAM_CURVE_PUBLIC_KEY
#   2. Local file: %APPDATA%\AntiScam\curve-public-key.txt  (or ~/.antiscam/curve-public-key.txt)
#   3. Empty string — first connection will be unencrypted; key received and saved from backend response.
import os as _os, pathlib as _pl

def _load_curve_public_key() -> str:
    """
    Load the backend CURVE server public key (Z85, 40 characters).

    Search order:
      1. Environment variable: ANTISCAM_CURVE_PUBLIC_KEY
      2. User-distributed key file: ~/.antiscam/curve-public-key.txt
      3. Backend-written public key file (same machine, dev/local):
           Windows: %LOCALAPPDATA%\\ASPS\\curve-server-public-key.txt
           Linux/macOS: ~/.asps/curve-server-public-key.txt

    If the key is not found in any source, startup is aborted with a clear
    error.  There is NO plaintext fallback — every connection must use CURVE.

    Key provisioning options:
      A) Same-machine dev: backend writes curve-server-public-key.txt on
         startup; the agent reads it automatically (source 3).
      B) Deployed install: embed the key in the installer and write it to
         ~/.antiscam/curve-public-key.txt (source 2).
      C) Environment variable: set ANTISCAM_CURVE_PUBLIC_KEY before launch
         (source 1).
    """
    import platform as _platform

    # 1. Environment variable
    env_key = _os.environ.get("ANTISCAM_CURVE_PUBLIC_KEY", "")
    if env_key:
        key = env_key.strip()
        if key:
            print("[CONFIG] CURVE public key loaded from environment variable")
            return key

    # 2. Manually distributed key file
    local_file = _pl.Path(_os.path.expanduser("~")) / ".antiscam" / "curve-public-key.txt"
    if local_file.exists():
        key = local_file.read_text().strip()
        if key:
            print(f"[CONFIG] CURVE public key loaded from: {local_file}")
            return key

    # 3. Backend public key file (same machine — dev/local setup).
    #    Backend writes ONLY the public key (Z85) to this file on startup.
    #    Python reads from here — no access to the full keypair / private key.
    if _platform.system() == 'Windows':
        backend_pubkey = _pl.Path(_os.environ.get('LOCALAPPDATA', _os.path.expanduser('~'))) / 'ASPS' / 'curve-server-public-key.txt'
    else:
        backend_pubkey = _pl.Path(_os.path.expanduser('~')) / '.asps' / 'curve-server-public-key.txt'

    if backend_pubkey.exists():
        try:
            key = backend_pubkey.read_text().strip()
            if key:
                print(f"[CONFIG] CURVE public key loaded from: {backend_pubkey}")
                return key
            # File exists but is empty — backend has CURVE disabled.
            # This is a configuration error: the agent requires CURVE.
            raise SystemExit(
                "[CONFIG] FATAL: CURVE public key file exists but is empty.\n"
                f"  File: {backend_pubkey}\n"
                "  The backend may have CURVE disabled (Security:CurveEnabled=false).\n"
                "  ASPS requires CURVE encryption. Enable CurveEnabled on the backend,\n"
                "  then restart the agent."
            )
        except SystemExit:
            raise
        except Exception as e:
            raise SystemExit(
                f"[CONFIG] FATAL: Could not read CURVE public key file: {e}\n"
                f"  File: {backend_pubkey}"
            ) from e

    # No key found in any source.
    raise SystemExit(
        "[CONFIG] FATAL: CURVE server public key not found.\n"
        "  The agent cannot start without the server's public key.\n"
        "  Provisioning options:\n"
        "    A) Same-machine (dev): start the backend first — it writes\n"
        f"       {backend_pubkey}\n"
        "    B) Deployed install: place the key in\n"
        "       ~/.antiscam/curve-public-key.txt\n"
        "    C) Environment variable: set ANTISCAM_CURVE_PUBLIC_KEY=<Z85 key>\n"
        "  Obtain the key from the backend startup log or from the admin UI.\n"
        "  There is NO plaintext fallback — all connections must use CURVE."
    )

BACKEND_SERVER_PUBLIC_KEY_Z85 = _load_curve_public_key()

# Whitelist - IPs and ports to ignore in remote access detection
WHITELIST_IPS = [
    "127.0.0.1",  # Local backend
    "100.86.253.55",  # Production server
]

WHITELIST_PORTS = [
    50001,  # Backend TCP port
    49569,  # Backend SignalR port
]

# Monitoring Settings
MONITOR_INTERVAL = 5  # seconds between checks (fast polling for quick detection)

# While in ImmediateDanger mode (between ImmediateDangerNotification and
# ImmediateDangerEndedNotification), the agent emits a RemoteAccessAlert on
# this cadence with the current BrowserTabs attached. This is in addition to
# the normal adaptive polling.
IMMEDIATE_DANGER_ALERT_INTERVAL_SECONDS = 10

# Main-loop poll interval while ImmediateDanger is active (overrides the normal
# adaptive interval of 5 s). Lower = faster tab-change detection by the backend.
IMMEDIATE_DANGER_POLL_INTERVAL_SECONDS = 3

# Default policy for including BrowserTabs in a RemoteAccessAlert.
# At runtime, the backend can temporarily override this default by sending a
# `SetBrowserTabsPolicyNotification` (see services.browser_tabs_policy). After
# the override's ValidUntil expires, the agent reverts to this default.
#
# Allowed values:
#   "incoming_only" (default) - include tabs only when remote-access direction
#                               is 'incoming' (someone controlling THIS device)
#   "always"                  - include with every RemoteAccessAlert
#   "never"                   - never include (disables the feature)
BROWSER_TABS_DEFAULT_MODE = "incoming_only"

# URLs to exclude from BrowserTabs in RemoteAccessAlerts.
# Tabs whose URL starts with (or exactly equals) any entry in this list are omitted.
# Include "" to drop tabs with an empty/missing URL.
# Override at runtime via BROWSER_TABS_URL_FILTER env var (comma-separated).
BROWSER_TABS_URL_FILTER: list = (
    _os.environ.get('BROWSER_TABS_URL_FILTER', '').split(',')
    if _os.environ.get('BROWSER_TABS_URL_FILTER')
    else ["", "localhost", "127.0.0.1"]
)

# Remote Access Apps to Monitor
class RemoteAccessApp:
    ANYDESK = 1
    TEAMVIEWER = 2
    CHROME_REMOTE_DESKTOP = 3
    REMOTEPC = 4
    LOGMEIN = 5
    SPLASHTOP = 6
    VNC = 7
    RDP = 8
    QUICK_ASSIST = 9
    CONNECTWISE = 10

REMOTE_APPS = {
    'anydesk': {
        'id': RemoteAccessApp.ANYDESK,
        'process_names': ['anydesk.exe', 'anydesk'],
        'listen_ports': [7070],
        'log_path_windows': r'~\AppData\Roaming\AnyDesk\ad.trace',
        'log_path_linux': '~/.anydesk/ad.trace',
        'log_path_mac': '~/Library/Application Support/AnyDesk/ad.trace',
        'connection_trace_windows': r'~\AppData\Roaming\AnyDesk\connection_trace.txt',
        'svc_trace_windows': r'%PROGRAMDATA%\AnyDesk\ad_svc.trace',
    },
    'teamviewer': {
        'id': RemoteAccessApp.TEAMVIEWER,
        'process_names': ['teamviewer.exe', 'teamviewer', 'teamviewerservice.exe'],
        'listen_ports': [5938],
        'service_names': ['TeamViewer'],
        'log_path_windows': r'~\AppData\Roaming\TeamViewer\TeamViewer15_Logfile.log',
        'log_path_linux': '~/.config/teamviewer/log',
        'log_path_mac': '~/Library/Logs/TeamViewer',
    },
    'rustdesk': {
        'id': RemoteAccessApp.VNC,
        'process_names': ['rustdesk.exe', 'rustdesk'],
    },
    'chrome_remote_desktop': {
        'id': RemoteAccessApp.CHROME_REMOTE_DESKTOP,
        'process_names': ['remoting_host.exe', 'chrome_remote_desktop'],
    },
    'quick_assist': {
        'id': RemoteAccessApp.QUICK_ASSIST,
        'process_names': ['quickassist.exe'],
    },
    'logmein': {
        'id': RemoteAccessApp.LOGMEIN,
        'process_names': ['logmein.exe', 'lmiguardiansvc.exe', 'logmeinrescue.exe'],
        'service_names': ['LogMeIn'],
    },
    'connectwise': {
        'id': RemoteAccessApp.CONNECTWISE,
        'process_names': ['screenconnect.clientservice.exe', 'screenconnect.windowsclient.exe'],
        'service_names': ['ScreenConnect Client'],
    },
    'remotepc': {
        # RemotePC by IDrive — common process names + service
        'id': RemoteAccessApp.REMOTEPC,
        'process_names': [
            'remotepc.exe',
            'remotepcservice.exe',
            'remotepcdesktop.exe',
            'remotepcagent.exe',
            'rpcservice.exe',
            'rpcsuite.exe',
        ],
        'service_names': ['RemotePCService', 'RemotePC'],
    },
    'splashtop': {
        # Splashtop Streamer (host) + client + agent
        'id': RemoteAccessApp.SPLASHTOP,
        'process_names': [
            'srserver.exe',         # main streamer
            'srstreamer.exe',       # alt streamer name
            'srmanager.exe',
            'srclient.exe',
            'splashtop.exe',
            'splashtopstreamer.exe',
            'stservice.exe',
        ],
        'listen_ports': [6783, 6784, 6785],
        'service_names': ['SSUService', 'Splashtop Streamer Remote Service'],
    },
    'rdp': {
        # Microsoft Remote Desktop (built-in Windows).
        # Note: the SERVER side is svchost hosting TermService — not detectable
        # by process name alone. We pick up:
        #   - mstsc.exe → outgoing RDP client (this user connecting OUT)
        #   - TermService running + listen on 3389 → server is enabled (incoming possible)
        # Limitation: an active incoming session without a named host process
        # may show is_running=False until session-helper processes appear
        # (RdpSa.exe, rdpclip.exe).
        'id': RemoteAccessApp.RDP,
        'process_names': [
            'mstsc.exe',            # RDP client (outgoing)
            'rdpclip.exe',           # clipboard helper inside RDP session
            'rdpsa.exe',             # RDP shadow / assistance
        ],
        'listen_ports': [3389],
        'service_names': ['TermService', 'UmRdpService', 'SessionEnv'],
    },
}

# Priority Levels
class Priority:
    LOW = 0
    MEDIUM = 1
    HIGH = 2
    CRITICAL = 3

# Operating Systems
class OperatingSystem:
    WINDOWS = 1
    LINUX = 2
    MAC = 3
    ANDROID = 4
    IOS = 5

# Connection Status
class ConnectionStatus:
    UNKNOWN = 0
    OPEN = 1
    CLOSED = 2

# Session Status (mirrors backend Common.Enums.SessionStatus)
class SessionStatus:
    UNKNOWN = 0
    OPEN = 1
    CLOSED = 2

# Risk Types
class RiskType:
    NONE = 0
    PHISHING = 1
    CLOAKING = 2
    IMPERSONATION = 3
    FAKE_DOM = 4
    UNKNOWN = 5

# Protective Actions
class ProtectiveAction:
    NONE = 0
    IGNORE = 1
    WARN_ON_SCREEN = 2
    MODAL_POPUP = 3
    BLOCK = 4

# Browser History Paths
BROWSER_HISTORY = {
    'chrome': {
        'windows': r'~\AppData\Local\Google\Chrome\User Data\Default\History',
        'linux': '~/.config/google-chrome/Default/History',
        'mac': '~/Library/Application Support/Google/Chrome/Default/History',
    },
    'edge': {
        'windows': r'~\AppData\Local\Microsoft\Edge\User Data\Default\History',
        'linux': '~/.config/microsoft-edge/Default/History',
        'mac': '~/Library/Application Support/Microsoft Edge/Default/History',
    },
    'firefox': {
        'windows': r'~\AppData\Roaming\Mozilla\Firefox\Profiles',
        'linux': '~/.mozilla/firefox',
        'mac': '~/Library/Application Support/Firefox/Profiles',
    },
}

# Local Storage
import platform

# Set data directory based on OS
if platform.system() == 'Windows':
    DATA_DIR = os.path.join(os.environ.get('APPDATA', os.path.expanduser('~')), 'AntiScam')
else:
    DATA_DIR = os.path.expanduser("~/.antiscam")

CACHE_FILE = "cache.json"
LOG_FILE = "events.jsonl"
CONFIG_FILE = "config.json"

# WebApi URL (for device registration page)
WEBAPI_URL = "http://localhost:5001"
#WEBAPI_URL = "https://admin.asps.io"  # AWS Production
#WEBAPI_URL = "http://100.88.78.75:5001"  # Old local server


# ─────────────────────────────────────────────────────────────────────────────
# Optional environment-specific overrides.
#
# `config_override.py` is generated at build time by:
#     python build_release.py --env <name>
# which copies `config_<name>.py` (e.g., config_dev.py, config_production.py)
# to `config_override.py`. It is gitignored and absent at runtime when not
# building a tagged environment — so the import must be tolerant.
#
# Any name defined in the override (BACKEND_HOST, WEBAPI_URL, ...) replaces
# the value above because Python applies the assignments in order and `*`-import
# rebinds the existing module attributes.
# ─────────────────────────────────────────────────────────────────────────────
try:
    from config_override import *  # noqa: F401, F403
except ImportError:
    pass
