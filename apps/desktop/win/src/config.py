"""
AntiScam Desktop App - Configuration
"""

import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


def _parse_bool(value: str) -> bool:
    """Parse string to boolean."""
    return value.lower() in ('true', '1', 'yes', 'on')


# Version
VERSION = "0.1.0"

# Debug Mode - defaults to False for production safety
DEBUG_MODE = _parse_bool(os.environ.get('DEBUG_MODE', 'false'))

# WebSocket Ports for Extension Communication (local)
EXTENSION_PORTS = [8080, 8181, 8282, 8383, 8484]

# Backend Server - ZeroMQ
BACKEND_HOST = "127.0.0.1"  # Local testing
#BACKEND_HOST = "100.88.78.75"  # Production server
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
    import json as _json, platform as _platform

    # 1. Environment variable
    env_key = _os.environ.get("ANTISCAM_CURVE_PUBLIC_KEY", "")
    if env_key:
        return env_key.strip()

    # 2. Manually distributed key file
    local_file = _pl.Path(_os.path.expanduser("~")) / ".antiscam" / "curve-public-key.txt"
    if local_file.exists():
        key = local_file.read_text().strip()
        if key:
            return key

    # 3. Backend keys file (same machine — dev/local setup).
    #    Backend saves its keypair here on first run; Python reads the public key
    #    so the first connection is already CURVE-encrypted.
    if _platform.system() == 'Windows':
        backend_keys = _pl.Path(_os.environ.get('LOCALAPPDATA', _os.path.expanduser('~'))) / 'ASPS' / 'curve-server-keys.json'
    else:
        backend_keys = _pl.Path(_os.path.expanduser('~')) / '.asps' / 'curve-server-keys.json'

    if backend_keys.exists():
        try:
            data = _json.loads(backend_keys.read_text())
            key = data.get('ServerPublicKeyZ85', '')
            if key:
                print(f"[CONFIG] CURVE public key loaded from backend keys file: {backend_keys}")
                return key
        except Exception as e:
            print(f"[CONFIG] Failed to read backend keys file: {e}")

    # 4. No key — first connection is unencrypted; key received from backend RequestToken response
    return ""

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
#WEBAPI_URL = "http://100.88.78.75:5001"  # Production server
