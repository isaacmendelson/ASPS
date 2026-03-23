"""
AntiScam Desktop App - Remote Access Monitor
Enhanced detection with real-time log parsing
Author: Tommy the Hacker 💀 + ASPS Team

Monitors: AnyDesk, TeamViewer, Chrome Remote Desktop, VNC, and more
Features:
- Real-time log file watching
- Multi-signal detection (process, network, logs, CPU)
- Direction detection (incoming/outgoing)
- GeoIP lookup
- File transfer detection
- Connection type detection (direct/relay)
"""

import os
import re
import sys
import time
import json
import threading
import urllib.request
from collections import deque
from datetime import datetime, timedelta
from typing import Dict, Optional, List, Tuple
from dataclasses import dataclass, field
from pathlib import Path
from enum import Enum
import logging

import psutil

from config import REMOTE_APPS, ConnectionStatus, WHITELIST_IPS, WHITELIST_PORTS, DEBUG_MODE
# REMOVED: parse_tool_logs - now using real-time SessionTracker only
# from detection.tools import get_tool_config
# from detection.log_parsers import parse_tool_logs
from detection.geolocation import get_geolocator

logger = logging.getLogger(__name__)

# ══════════════════════════════════════════════════════════════════════════════
# CONFIGURATION
# ══════════════════════════════════════════════════════════════════════════════

IS_WINDOWS = sys.platform == "win32"

class MonitorConfig:
    """Central configuration for monitoring."""
    
    APPDATA = os.environ.get("APPDATA", "C:\\Users\\Default\\AppData\\Roaming")
    PROGRAMDATA = os.environ.get("PROGRAMDATA", "C:\\ProgramData")
    
    # AnyDesk paths
    SVC_TRACE = Path(PROGRAMDATA) / "AnyDesk" / "ad_svc.trace"
    UI_TRACE = Path(APPDATA) / "AnyDesk" / "ad.trace"
    CONN_TRACE = Path(APPDATA) / "AnyDesk" / "connection_trace.txt"
    
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


# ══════════════════════════════════════════════════════════════════════════════
# DATA MODELS
# ══════════════════════════════════════════════════════════════════════════════

@dataclass
class RemoteAppStatus:
    """Status of a remote access application - compatible with existing API."""
    app_name: str
    app_id: int
    is_running: bool
    has_active_session: bool
    process_count: int
    connection_count: int
    connection_status: int
    remote_ip: Optional[str] = None
    direction: Optional[str] = None  # 'incoming', 'outgoing', 'unknown'
    confidence: str = 'low'  # 'low', 'medium', 'high'
    remote_country: Optional[str] = None
    remote_country_code: Optional[str] = None
    # New fields from enhanced detection
    remote_os: Optional[str] = None
    remote_version: Optional[str] = None
    connection_type: Optional[str] = None  # 'direct' or 'relay'
    file_transfer_active: bool = False
    file_transfers: int = 0


@dataclass
class StateChange:
    """Represents a state change event for a remote access app."""
    app_name: str
    change_type: str  # 'opened', 'closed', 'session_started', 'session_ended'
    timestamp: datetime
    status: RemoteAppStatus
    late_detection: bool = False


class SessionDirection:
    INCOMING = "incoming"
    OUTGOING = "outgoing"
    UNKNOWN = "unknown"


# ══════════════════════════════════════════════════════════════════════════════
# GEOIP LOOKUP
# ══════════════════════════════════════════════════════════════════════════════

class GeoIPLookup:
    """GeoIP lookup using free ip-api.com service."""
    
    _cache: Dict[str, dict] = {}
    
    @classmethod
    def lookup(cls, ip: str) -> dict:
        """
        Lookup GeoIP info for an IP address.
        Returns: {"country": "...", "country_code": "...", "city": "...", "isp": "..."}
        """
        if not ip or ip == "?" or cls._is_private_ip(ip):
            return {}
        
        if ip in cls._cache:
            return cls._cache[ip]
        
        try:
            url = f"http://ip-api.com/json/{ip}?fields=status,country,countryCode,city,isp"
            with urllib.request.urlopen(url, timeout=3) as resp:
                data = json.loads(resp.read().decode())
                if data.get("status") == "success":
                    result = {
                        "country": data.get("country", ""),
                        "country_code": data.get("countryCode", ""),
                        "city": data.get("city", ""),
                        "isp": data.get("isp", ""),
                    }
                    cls._cache[ip] = result
                    return result
        except Exception as e:
            logger.debug(f"GeoIP lookup failed for {ip}: {e}")
        
        return {}
    
    @staticmethod
    def _is_private_ip(ip: str) -> bool:
        """Check if IP is private/local."""
        return (
            ip.startswith("10.") or
            ip.startswith("192.168.") or
            ip.startswith("172.16.") or
            ip.startswith("172.17.") or
            ip.startswith("172.18.") or
            ip.startswith("172.19.") or
            ip.startswith("172.2") or
            ip.startswith("172.30.") or
            ip.startswith("172.31.") or
            ip.startswith("127.") or
            ip.startswith("100.") or  # Tailscale
            ip == "::1"
        )


# ══════════════════════════════════════════════════════════════════════════════
# LOG PARSER - Enhanced regex patterns
# ══════════════════════════════════════════════════════════════════════════════

class LogParser:
    """
    Parses AnyDesk/TeamViewer/VNC trace files for session events.
    Enhanced with comprehensive regex patterns.
    """

    # Timestamp patterns
    RE_TIMESTAMP = re.compile(r'(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})')
    
    # ─── AnyDesk patterns ─────────────────────────────────────────────────────
    
    # Incoming request: "Incoming session request: - (1458399339)"
    RE_INCOMING = re.compile(
        r'[Ii]ncoming\s+session\s+request.*?\((\d+)\)',
        re.IGNORECASE
    )
    
    # Incoming connection with IP: "[100.87.30.66:51533] Incoming connection"
    RE_INCOMING_IP = re.compile(
        r'\[(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):\d+\]\s*[Ii]ncoming\s+connection',
        re.IGNORECASE
    )
    
    # Session started
    RE_STARTED = re.compile(
        r'[Ss]ession\s+started'
        r'(?:.*?(?:client_id|id)\s*=\s*(\d+))?',
        re.IGNORECASE
    )
    
    # Client connected from IP (old format)
    RE_CLIENT_IP = re.compile(
        r'[Cc]lient\s+connected\s+from\s+([\d\.]+)',
        re.IGNORECASE
    )
    
    # Session stopped/ended
    RE_STOPPED = re.compile(
        r'[Ss]ession\s+(?:stopped|ended|closed|disconnected)',
        re.IGNORECASE
    )
    
    # Outgoing connection
    RE_OUTGOING = re.compile(
        r'[Cc]onnecting\s+to\s+(\d+)',
        re.IGNORECASE
    )
    
    # Remote OS
    RE_REMOTE_OS = re.compile(
        r'Remote\s+OS:\s*(\w+)',
        re.IGNORECASE
    )
    
    # Remote version
    RE_REMOTE_VERSION = re.compile(
        r'Remote\s+version:\s*([\d\.]+)',
        re.IGNORECASE
    )
    
    # File transfer
    RE_FILE_TRANSFER_START = re.compile(
        r'local_file_transfer.*(?:Starting|Started|Initializing)',
        re.IGNORECASE
    )
    RE_FILE_TRANSFER_STOP = re.compile(
        r'local_file_transfer.*(?:Stopping|stopped|completed|finished)',
        re.IGNORECASE
    )
    
    # Connection type
    RE_CONN_TYPE_DIRECT = re.compile(
        r'(?:direct\s+connection|connection.*direct|Route\s+type:\s*direct)',
        re.IGNORECASE
    )
    RE_CONN_TYPE_RELAY = re.compile(
        r'(?:relay|tunnel|Route\s+type:\s*tunnel)',
        re.IGNORECASE
    )
    
    # connection_trace.txt patterns
    RE_CONN_TRACE_IN = re.compile(
        r'[Ii]ncoming\s+(\d{4}-\d{2}-\d{2}),?\s+(\d{2}:\d{2})\s+[Uu]ser\s+(\d+)',
        re.IGNORECASE
    )
    RE_CONN_TRACE_OUT = re.compile(
        r'[Oo]utgoing\s+(\d{4}-\d{2}-\d{2}),?\s+(\d{2}:\d{2})\s+[Uu]ser\s+(\d+)',
        re.IGNORECASE
    )
    
    # ─── TeamViewer patterns ──────────────────────────────────────────────────
    
    RE_TV_INCOMING = re.compile(
        r'[Ii]ncoming\s+(?:connection|session).*?(?:Partner\s*ID|from)[:\s]*(\d+)',
        re.IGNORECASE
    )
    RE_TV_DISCONNECT = re.compile(
        r'(?:session|connection)\s+(?:ended|closed|terminated|disconnected)',
        re.IGNORECASE
    )
    
    # ─── VNC patterns ─────────────────────────────────────────────────────────
    
    RE_VNC_ACCEPT = re.compile(
        r'Connections:\s+Accepted:\s+(\[?[\d:a-fA-F\.]+\]?)::(\d+)',
        re.IGNORECASE
    )
    RE_VNC_CLOSE = re.compile(
        r'Connections:\s+Closed:\s+(\[?[\d:a-fA-F\.]+\]?)::(\d+)',
        re.IGNORECASE
    )

    @staticmethod
    def parse_timestamp(text: str) -> Optional[datetime]:
        m = LogParser.RE_TIMESTAMP.search(text)
        if m:
            try:
                return datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S")
            except ValueError:
                pass
        return None

    def parse_line(self, line: str) -> Optional[dict]:
        """Parse a single log line. Returns dict with event info, or None."""
        ts = self.parse_timestamp(line)

        # Incoming session request
        m = self.RE_INCOMING.search(line)
        if m:
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": m.group(1) or "",
                "remote_ip": "",
                "raw": line.strip(),
            }

        # Session started
        m = self.RE_STARTED.search(line)
        if m:
            return {
                "event": "session_started",
                "timestamp": ts,
                "remote_id": m.group(1) or "",
                "raw": line.strip(),
            }

        # Client connected from IP (old format)
        m = self.RE_CLIENT_IP.search(line)
        if m:
            return {
                "event": "client_ip",
                "timestamp": ts,
                "remote_ip": m.group(1),
                "raw": line.strip(),
            }
        
        # Incoming connection with IP (new format)
        m = self.RE_INCOMING_IP.search(line)
        if m:
            return {
                "event": "client_ip",
                "timestamp": ts,
                "remote_ip": m.group(1),
                "raw": line.strip(),
            }

        # Session stopped
        m = self.RE_STOPPED.search(line)
        if m:
            return {
                "event": "session_stopped",
                "timestamp": ts,
                "raw": line.strip(),
            }

        # Outgoing connection
        m = self.RE_OUTGOING.search(line)
        if m:
            return {
                "event": "outgoing_start",
                "timestamp": ts,
                "remote_id": m.group(1),
                "raw": line.strip(),
            }

        # Remote OS
        m = self.RE_REMOTE_OS.search(line)
        if m:
            return {
                "event": "remote_info",
                "timestamp": ts,
                "remote_os": m.group(1),
                "raw": line.strip(),
            }
        
        # Remote version
        m = self.RE_REMOTE_VERSION.search(line)
        if m:
            return {
                "event": "remote_info",
                "timestamp": ts,
                "remote_version": m.group(1),
                "raw": line.strip(),
            }

        # File transfer
        if self.RE_FILE_TRANSFER_START.search(line):
            return {"event": "file_transfer_start", "timestamp": ts, "raw": line.strip()}
        
        if self.RE_FILE_TRANSFER_STOP.search(line):
            return {"event": "file_transfer_stop", "timestamp": ts, "raw": line.strip()}

        # Connection type
        if self.RE_CONN_TYPE_DIRECT.search(line):
            return {"event": "connection_type", "timestamp": ts, "conn_type": "direct", "raw": line.strip()}
        
        if self.RE_CONN_TYPE_RELAY.search(line):
            return {"event": "connection_type", "timestamp": ts, "conn_type": "relay", "raw": line.strip()}

        # ─── TeamViewer patterns ──────────────────────────────────────────────
        
        # TeamViewer incoming connection
        m = self.RE_TV_INCOMING.search(line)
        if m:
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": m.group(1) or "",
                "remote_ip": "",
                "raw": line.strip(),
            }
        
        # TeamViewer disconnect
        if self.RE_TV_DISCONNECT.search(line):
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}
        
        # ─── VNC patterns ─────────────────────────────────────────────────────
        
        # VNC connection accepted
        m = self.RE_VNC_ACCEPT.search(line)
        if m:
            ip = m.group(1).strip('[]')  # Remove brackets from IPv6
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": "",
                "remote_ip": ip,
                "raw": line.strip(),
            }
        
        # VNC connection closed
        m = self.RE_VNC_CLOSE.search(line)
        if m:
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}
        
        # ─── Chrome Remote Desktop patterns ───────────────────────────────────
        
        # CRD client connected
        if re.search(r'client\s+connected|incoming\s+connection|session\s+started', line, re.IGNORECASE):
            # Try to extract IP if present
            ip_match = re.search(r'(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})', line)
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": "",
                "remote_ip": ip_match.group(1) if ip_match else "",
                "raw": line.strip(),
            }
        
        # CRD client disconnected
        if re.search(r'client\s+disconnected|session\s+ended|connection\s+closed', line, re.IGNORECASE):
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}

        return None


# ══════════════════════════════════════════════════════════════════════════════
# LOG WATCHER - Real-time log file tailing
# ══════════════════════════════════════════════════════════════════════════════

class LogWatcher:
    """Tails a log file and yields new lines as they appear."""

    def __init__(self, path: Path, poll_interval: float = 1.0):
        self.path = path
        self.poll_interval = poll_interval
        self._stop = threading.Event()
        self._pos = 0

    def _seek_end(self):
        """Start from end of file (don't replay history)."""
        if self.path.exists():
            self._pos = self.path.stat().st_size
        else:
            self._pos = 0

    def tail(self, from_start: bool = False):
        """Generator: yields new lines from the log file."""
        if not from_start:
            self._seek_end()

        while not self._stop.is_set():
            if not self.path.exists():
                time.sleep(self.poll_interval)
                continue

            current_size = self.path.stat().st_size
            if current_size < self._pos:
                # File was rotated/truncated
                self._pos = 0

            if current_size > self._pos:
                try:
                    with open(self.path, "r", encoding="utf-8", errors="replace") as f:
                        f.seek(self._pos)
                        new_data = f.read()
                        self._pos = f.tell()
                    for line in new_data.splitlines():
                        if line.strip():
                            yield line
                except (IOError, PermissionError):
                    pass

            time.sleep(self.poll_interval)

    def stop(self):
        self._stop.set()


# ══════════════════════════════════════════════════════════════════════════════
# SESSION TRACKER - State machine for session tracking
# ══════════════════════════════════════════════════════════════════════════════

class SessionState:
    """Tracks the state of a single session."""
    
    def __init__(self):
        self.direction: str = SessionDirection.UNKNOWN
        self.remote_id: str = ""
        self.remote_ip: str = ""
        self.remote_os: str = ""
        self.remote_version: str = ""
        self.connection_type: str = ""
        self.file_transfer_active: bool = False
        self.file_transfers: int = 0
        self.start_time: Optional[datetime] = None
        self.end_time: Optional[datetime] = None
        self.active: bool = False
        self.geoip: dict = {}


class SessionTracker:
    """Tracks session state based on log events."""

    def __init__(self):
        self.current: Optional[SessionState] = None
        self.history: List[SessionState] = []
        self._lock = threading.Lock()
        self._pending_ip: str = ""

    def on_event(self, event: dict) -> Optional[str]:
        """
        Process a parsed log event.
        Returns event type if state changed: 'session_started', 'session_ended', etc.
        """
        with self._lock:
            etype = event.get("event")

            if etype == "incoming_request":
                return self._start_incoming(event)

            elif etype == "outgoing_start":
                return self._start_outgoing(event)

            elif etype == "session_started":
                return self._session_started(event)

            elif etype == "client_ip":
                self._update_ip(event)
                return None

            elif etype == "session_stopped":
                return self._session_stopped(event)

            elif etype == "remote_info":
                self._update_remote_info(event)
                return None

            elif etype == "file_transfer_start":
                self._file_transfer_start()
                return None

            elif etype == "file_transfer_stop":
                self._file_transfer_stop()
                return None

            elif etype == "connection_type":
                self._update_connection_type(event)
                return None

        return None

    def _start_incoming(self, event: dict) -> str:
        sess = SessionState()
        sess.direction = SessionDirection.INCOMING
        sess.remote_id = event.get("remote_id", "")
        sess.remote_ip = event.get("remote_ip", "") or self._pending_ip
        self._pending_ip = ""
        sess.start_time = event.get("timestamp") or datetime.now()
        sess.active = True
        
        # GeoIP lookup
        if sess.remote_ip:
            sess.geoip = GeoIPLookup.lookup(sess.remote_ip)
        
        self.current = sess
        return "session_started"

    def _start_outgoing(self, event: dict) -> str:
        sess = SessionState()
        sess.direction = SessionDirection.OUTGOING
        sess.remote_id = event.get("remote_id", "")
        sess.start_time = event.get("timestamp") or datetime.now()
        sess.active = True
        self.current = sess
        return "session_started"

    def _session_started(self, event: dict) -> Optional[str]:
        if self.current:
            if not self.current.remote_id and event.get("remote_id"):
                self.current.remote_id = event["remote_id"]
            return None
        else:
            sess = SessionState()
            sess.direction = SessionDirection.UNKNOWN
            sess.remote_id = event.get("remote_id", "")
            sess.start_time = event.get("timestamp") or datetime.now()
            sess.active = True
            self.current = sess
            return "session_started"

    def _update_ip(self, event: dict):
        ip = event.get("remote_ip", "")
        if self.current and not self.current.remote_ip:
            self.current.remote_ip = ip
            if ip:
                self.current.geoip = GeoIPLookup.lookup(ip)
        elif ip:
            self._pending_ip = ip

    def _update_remote_info(self, event: dict):
        if self.current:
            if event.get("remote_os") and not self.current.remote_os:
                self.current.remote_os = event["remote_os"]
            if event.get("remote_version") and not self.current.remote_version:
                self.current.remote_version = event["remote_version"]

    def _file_transfer_start(self):
        if self.current:
            self.current.file_transfer_active = True
            self.current.file_transfers += 1

    def _file_transfer_stop(self):
        if self.current:
            self.current.file_transfer_active = False

    def _update_connection_type(self, event: dict):
        if self.current and not self.current.connection_type:
            self.current.connection_type = event.get("conn_type", "")

    def _session_stopped(self, event: dict) -> str:
        if self.current:
            self.current.end_time = event.get("timestamp") or datetime.now()
            self.current.active = False
            self.history.append(self.current)
            self.current = None
        return "session_ended"

    def get_current_session(self) -> Optional[SessionState]:
        with self._lock:
            return self.current

    def has_active_session(self) -> bool:
        with self._lock:
            return self.current is not None and self.current.active


# ══════════════════════════════════════════════════════════════════════════════
# DETECTION HISTORY
# ══════════════════════════════════════════════════════════════════════════════

class DetectionHistory:
    """Rolling log of detection events for debugging."""

    def __init__(self, max_events: int = 100):
        self._events: deque = deque(maxlen=max_events)

    def add(self, state_change: StateChange):
        self._events.append({
            'timestamp': state_change.timestamp.isoformat(),
            'app': state_change.app_name,
            'type': state_change.change_type,
            'late_detection': state_change.late_detection,
            'process_count': state_change.status.process_count,
            'has_session': state_change.status.has_active_session,
            'remote_ip': state_change.status.remote_ip
        })

    def get_history(self) -> List[dict]:
        return list(reversed(self._events))


# ══════════════════════════════════════════════════════════════════════════════
# DEBOUNCED STATE TRACKER
# ══════════════════════════════════════════════════════════════════════════════

class DebouncedStateTracker:
    """Tracks state changes with debouncing for close events."""

    def __init__(self, close_debounce_seconds: float = 3.0, session_end_debounce_seconds: float = 10.0):
        self._close_debounce = close_debounce_seconds
        self._session_end_debounce = session_end_debounce_seconds
        self._pending_closes: Dict[str, float] = {}
        self._pending_session_ends: Dict[str, float] = {}
        self._previous_state: Dict[str, RemoteAppStatus] = {}

    def process_state(self, app_name: str, current_status: RemoteAppStatus) -> Optional[StateChange]:
        """Process current state and return a StateChange if a transition occurred."""
        prev = self._previous_state.get(app_name)
        now = datetime.now()

        # App just closed - schedule pending close
        if prev and prev.is_running and not current_status.is_running:
            self._pending_closes[app_name] = time.time()
            self._pending_session_ends.pop(app_name, None)
            self._previous_state[app_name] = current_status
            return None

        # App running but was in pending_closes - cancel pending close
        if current_status.is_running and app_name in self._pending_closes:
            del self._pending_closes[app_name]
            self._pending_session_ends.pop(app_name, None)
            self._previous_state[app_name] = current_status
            return None

        # App just opened
        if current_status.is_running and (not prev or not prev.is_running):
            self._previous_state[app_name] = current_status
            return StateChange(
                app_name=app_name,
                change_type='opened',
                timestamp=now,
                status=current_status
            )

        # Session state changes (while app is running)
        if prev and current_status.is_running and prev.is_running:
            # Session just started
            if current_status.has_active_session and not prev.has_active_session:
                self._pending_session_ends.pop(app_name, None)
                self._previous_state[app_name] = current_status
                return StateChange(
                    app_name=app_name,
                    change_type='session_started',
                    timestamp=now,
                    status=current_status
                )
            # Session just ended - schedule pending session end
            if not current_status.has_active_session and prev.has_active_session:
                self._pending_session_ends[app_name] = time.time()
                self._previous_state[app_name] = current_status
                return None

        self._previous_state[app_name] = current_status
        return None

    def check_pending_events(self) -> List[StateChange]:
        """Check for debounced events that have completed their debounce period."""
        now = time.time()
        completed_events: List[StateChange] = []

        # Check pending closes
        for app_name, close_time in list(self._pending_closes.items()):
            if now - close_time >= self._close_debounce:
                prev_status = self._previous_state.get(app_name)
                if prev_status:
                    completed_events.append(StateChange(
                        app_name=app_name,
                        change_type='closed',
                        timestamp=datetime.now(),
                        status=prev_status
                    ))
                del self._pending_closes[app_name]
                self._pending_session_ends.pop(app_name, None)

        # Check pending session ends
        for app_name, end_time in list(self._pending_session_ends.items()):
            if now - end_time >= self._session_end_debounce:
                prev_status = self._previous_state.get(app_name)
                if prev_status:
                    completed_events.append(StateChange(
                        app_name=app_name,
                        change_type='session_ended',
                        timestamp=datetime.now(),
                        status=prev_status
                    ))
                del self._pending_session_ends[app_name]

        return completed_events


# ══════════════════════════════════════════════════════════════════════════════
# CONFIDENCE CALCULATION
# ══════════════════════════════════════════════════════════════════════════════

def calculate_confidence(signals: dict) -> str:
    """Calculate confidence level based on detection signals."""
    score = 0
    
    if signals.get('active_connection'):
        score += 3
    if signals.get('log_session_active'):
        score += 3
    if signals.get('cpu_active'):
        score += 1
    if signals.get('service_running'):
        score += 1
    
    if score >= 4:
        return 'high'
    elif score >= 2:
        return 'medium'
    else:
        return 'low'


# ══════════════════════════════════════════════════════════════════════════════
# REMOTE ACCESS MONITOR - Main class
# ══════════════════════════════════════════════════════════════════════════════

class RemoteAccessMonitor:
    """
    Main monitoring class - compatible with existing API.
    Uses enhanced detection with real-time log parsing.
    """

    def __init__(self):
        self.system = sys.platform
        self._last_status: Dict[str, RemoteAppStatus] = {}
        self._state_tracker = DebouncedStateTracker(close_debounce_seconds=3, session_end_debounce_seconds=10)
        self._history = DetectionHistory(max_events=100)
        
        # Enhanced tracking per app
        self._session_trackers: Dict[str, SessionTracker] = {}
        self._log_watchers: Dict[str, List[LogWatcher]] = {}
        self._log_parser = LogParser()
        self._watcher_threads: List[threading.Thread] = []
        self._running = False
        
        print(f"[REMOTE-MONITOR] Initialized on {self.system}")

    def start_realtime_monitoring(self):
        """Start real-time log monitoring in background threads."""
        if self._running:
            return
        
        self._running = True
        
        # Start watchers for all supported apps
        self._start_anydesk_watchers()
        self._start_teamviewer_watchers()
        self._start_vnc_watchers()
        self._start_crd_watchers()
        
        print("[REMOTE-MONITOR] Real-time monitoring started for all apps")

    def _start_anydesk_watchers(self):
        """Start watching AnyDesk log files."""
        app_name = 'anydesk'
        self._session_trackers[app_name] = SessionTracker()
        self._log_watchers[app_name] = []
        
        log_paths = [
            MonitorConfig.SVC_TRACE,
            MonitorConfig.UI_TRACE,
            MonitorConfig.CONN_TRACE,
        ]
        
        for path in log_paths:
            watcher = LogWatcher(path, MonitorConfig.POLL_INTERVAL)
            self._log_watchers[app_name].append(watcher)
            
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()
            
            if DEBUG_MODE:
                print(f"[REMOTE-MONITOR] Watching: {path}")

    def _start_teamviewer_watchers(self):
        """Start watching TeamViewer log files."""
        app_name = 'teamviewer'
        self._session_trackers[app_name] = SessionTracker()
        self._log_watchers[app_name] = []
        
        # TeamViewer log paths
        log_paths = [
            MonitorConfig.TV_CONNECTIONS_IN,
            MonitorConfig.TV_CONNECTIONS_OUT,
        ]
        
        # Also check for TeamViewer logfile (version-specific)
        tv_logfile = Path(MonitorConfig.APPDATA) / "TeamViewer" / "TeamViewer15_Logfile.log"
        if tv_logfile.exists():
            log_paths.append(tv_logfile)
        
        for path in log_paths:
            if not path.exists():
                if DEBUG_MODE:
                    print(f"[REMOTE-MONITOR] TeamViewer log not found: {path}")
                continue
                
            watcher = LogWatcher(path, MonitorConfig.POLL_INTERVAL)
            self._log_watchers[app_name].append(watcher)
            
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()
            
            if DEBUG_MODE:
                print(f"[REMOTE-MONITOR] Watching TeamViewer: {path}")

    def _start_vnc_watchers(self):
        """Start watching VNC log files."""
        app_name = 'vnc'
        self._session_trackers[app_name] = SessionTracker()
        self._log_watchers[app_name] = []
        
        # VNC log paths vary by implementation
        vnc_log_paths = [
            # TigerVNC on Windows
            Path(MonitorConfig.PROGRAMDATA) / "TigerVNC" / "tigervnc.log",
            # UltraVNC
            Path("C:/Program Files/uvnc bvba/UltraVNC/ultravnc.log"),
            Path("C:/Program Files (x86)/uvnc bvba/UltraVNC/ultravnc.log"),
            # TightVNC
            Path(MonitorConfig.PROGRAMDATA) / "TightVNC" / "tvnserver.log",
        ]
        
        for path in vnc_log_paths:
            if not path.exists():
                continue
                
            watcher = LogWatcher(path, MonitorConfig.POLL_INTERVAL)
            self._log_watchers[app_name].append(watcher)
            
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()
            
            if DEBUG_MODE:
                print(f"[REMOTE-MONITOR] Watching VNC: {path}")

    def _start_crd_watchers(self):
        """Start watching Chrome Remote Desktop log files."""
        app_name = 'chrome_remote_desktop'
        self._session_trackers[app_name] = SessionTracker()
        self._log_watchers[app_name] = []
        
        # Chrome Remote Desktop log directory
        crd_log_dir = Path(MonitorConfig.APPDATA) / "Google" / "Chrome Remote Desktop" / "logs"
        
        if crd_log_dir.exists():
            # Watch all .log files in the directory
            for log_file in crd_log_dir.glob("*.log"):
                watcher = LogWatcher(log_file, MonitorConfig.POLL_INTERVAL)
                self._log_watchers[app_name].append(watcher)
                
                t = threading.Thread(
                    target=self._watch_log,
                    args=(app_name, watcher),
                    daemon=True
                )
                self._watcher_threads.append(t)
                t.start()
                
                if DEBUG_MODE:
                    print(f"[REMOTE-MONITOR] Watching CRD: {log_file}")
        elif DEBUG_MODE:
            print(f"[REMOTE-MONITOR] Chrome Remote Desktop logs not found: {crd_log_dir}")

    def _watch_log(self, app_name: str, watcher: LogWatcher):
        """Thread: watch a single log file."""
        tracker = self._session_trackers.get(app_name)
        if not tracker:
            return
        
        for line in watcher.tail(from_start=False):
            if not self._running:
                break
            
            event = self._log_parser.parse_line(line)
            if event:
                change_type = tracker.on_event(event)
                if change_type and DEBUG_MODE:
                    print(f"[REMOTE-MONITOR] {app_name}: {change_type}")

    def stop_realtime_monitoring(self):
        """Stop real-time monitoring."""
        self._running = False
        for watchers in self._log_watchers.values():
            for w in watchers:
                w.stop()

    def find_processes(self, app_name: str) -> List[psutil.Process]:
        """Find all processes for a given app."""
        app_config = REMOTE_APPS.get(app_name)
        if not app_config:
            return []

        process_names = app_config.get('process_names', [])
        found = []

        for proc in psutil.process_iter(['pid', 'name']):
            try:
                proc_name = proc.info['name'].lower()
                if any(pn.lower() in proc_name for pn in process_names):
                    found.append(proc)
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                continue

        return found

    def _scan_all_processes(self) -> Dict[str, List[psutil.Process]]:
        """Single process_iter() call -- returns {app_name: [processes]}."""
        process_to_apps: Dict[str, List[str]] = {}
        for app_name, app_config in REMOTE_APPS.items():
            for pn in app_config.get('process_names', []):
                process_to_apps.setdefault(pn.lower(), []).append(app_name)

        result: Dict[str, List[psutil.Process]] = {app_name: [] for app_name in REMOTE_APPS}

        for proc in psutil.process_iter(['pid', 'name']):
            try:
                proc_name = proc.info['name'].lower()
                for target_name, app_names in process_to_apps.items():
                    if target_name in proc_name:
                        for app_name in app_names:
                            result[app_name].append(proc)
            except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                continue

        return result

    def is_whitelisted(self, ip: str, port: int) -> bool:
        """Check if connection is whitelisted."""
        if ip in WHITELIST_IPS:
            return True
        if port in WHITELIST_PORTS:
            return True
        return False

    def check_suspicious_connections(self, processes: List[psutil.Process], app_name: str) -> tuple:
        """Check for suspicious network connections."""
        total_connections = 0
        suspicious_count = 0
        remote_ip = None

        for proc in processes:
            try:
                connections = proc.net_connections()
                total_connections += len(connections)

                for conn in connections:
                    if conn.status == 'ESTABLISHED' and conn.raddr:
                        ip = conn.raddr.ip
                        port = conn.raddr.port

                        if ip.startswith('127.') or ip == '::1':
                            continue
                        if self.is_whitelisted(ip, port):
                            continue
                        if port in MonitorConfig.INFRASTRUCTURE_PORTS:
                            continue

                        suspicious_count += 1
                        remote_ip = ip

            except (psutil.AccessDenied, AttributeError):
                continue

        return total_connections, suspicious_count, remote_ip

    def check_cpu_usage(self, processes: List[psutil.Process], app_name: str) -> float:
        """Check total CPU usage of processes."""
        total_cpu = 0.0
        for proc in processes:
            try:
                cpu = proc.cpu_percent(interval=0.3)
                total_cpu += cpu
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return total_cpu

    def check_windows_service(self, service_name: str) -> bool:
        """Check if Windows service is running."""
        try:
            service = psutil.win_service_get(service_name)
            return service.status() == 'running'
        except (psutil.NoSuchProcess, Exception):
            return False

    def _check_app_with_processes(self, app_name: str, processes: List[psutil.Process]) -> RemoteAppStatus:
        """Check status of a specific app using pre-scanned process list."""
        app_config = REMOTE_APPS.get(app_name)
        if not app_config:
            return RemoteAppStatus(
                app_name=app_name, app_id=0, is_running=False,
                has_active_session=False, process_count=0,
                connection_count=0, connection_status=ConnectionStatus.UNKNOWN
            )

        if not processes:
            return RemoteAppStatus(
                app_name=app_name, app_id=app_config['id'], is_running=False,
                has_active_session=False, process_count=0,
                connection_count=0, connection_status=ConnectionStatus.CLOSED
            )

        # Check connections
        total_conn, suspicious_conn, remote_ip = self.check_suspicious_connections(processes, app_name)
        
        # Check CPU
        cpu_usage = self.check_cpu_usage(processes, app_name)
        
        # Check session tracker for log-based session info (real-time only)
        # NOTE: We use ONLY the real-time SessionTracker, not periodic file parsing
        session_tracker = self._session_trackers.get(app_name)
        session = session_tracker.get_current_session() if session_tracker else None
        
        log_session_active = session is not None and session.active
        log_remote_ip = session.remote_ip if session else None
        log_direction = session.direction if session else SessionDirection.UNKNOWN

        # Check Windows service if configured
        service_running = False
        service_names = app_config.get('service_names', [])
        if service_names:
            for svc_name in service_names:
                if self.check_windows_service(svc_name):
                    service_running = True
                    break

        # Build signals for confidence calculation
        signals = {
            'active_connection': suspicious_conn > 0,
            'log_session_active': log_session_active,
            'cpu_active': cpu_usage > 5.0,
            'service_running': service_running
        }

        confidence = calculate_confidence(signals)
        has_active_session = any([
            signals['active_connection'],
            signals['log_session_active'],
            signals['cpu_active']
        ])

        final_remote_ip = remote_ip or log_remote_ip
        conn_status = ConnectionStatus.OPEN if has_active_session else ConnectionStatus.CLOSED

        # GeoIP lookup using detection module
        remote_country = None
        remote_country_code = None
        if final_remote_ip:
            try:
                geo = get_geolocator()
                country_info = geo.get_country(final_remote_ip)
                remote_country = country_info.get('country')
                remote_country_code = country_info.get('country_code')
            except Exception:
                # Fallback to internal GeoIPLookup
                geo_result = GeoIPLookup.lookup(final_remote_ip)
                remote_country = geo_result.get('country')
                remote_country_code = geo_result.get('country_code')

        # Build status with enhanced fields
        status = RemoteAppStatus(
            app_name=app_name,
            app_id=app_config['id'],
            is_running=True,
            has_active_session=has_active_session,
            process_count=len(processes),
            connection_count=total_conn,
            connection_status=conn_status,
            remote_ip=final_remote_ip,
            direction=log_direction,
            confidence=confidence,
            remote_country=remote_country,
            remote_country_code=remote_country_code,
            # Enhanced fields from session tracker
            remote_os=session.remote_os if session else None,
            remote_version=session.remote_version if session else None,
            connection_type=session.connection_type if session else None,
            file_transfer_active=session.file_transfer_active if session else False,
            file_transfers=session.file_transfers if session else 0,
        )

        self._last_status[app_name] = status
        return status

    def check_app(self, app_name: str) -> RemoteAppStatus:
        """Check status of a specific remote access app."""
        processes = self.find_processes(app_name)
        return self._check_app_with_processes(app_name, processes)

    def check_all(self) -> Dict[str, RemoteAppStatus]:
        """Check all monitored remote access apps with a single process scan."""
        processes_by_app = self._scan_all_processes()
        results = {}
        for app_name in REMOTE_APPS.keys():
            results[app_name] = self._check_app_with_processes(
                app_name, processes_by_app.get(app_name, [])
            )
        return results

    def check_all_with_changes(self) -> Tuple[Dict[str, RemoteAppStatus], List[StateChange]]:
        """Check all apps and return state changes."""
        results = self.check_all()
        changes: List[StateChange] = []

        for app_name, status in results.items():
            change = self._state_tracker.process_state(app_name, status)
            if change:
                self._history.add(change)
                changes.append(change)

        pending_events = self._state_tracker.check_pending_events()
        for event in pending_events:
            self._history.add(event)
            changes.append(event)

        return results, changes

    def get_active_sessions(self) -> List[RemoteAppStatus]:
        """Get list of apps with active sessions."""
        all_status = self.check_all()
        return [s for s in all_status.values() if s.has_active_session]

    def has_any_active_session(self) -> bool:
        """Quick check if any remote app has active session."""
        for app_name in REMOTE_APPS.keys():
            status = self.check_app(app_name)
            if status.has_active_session:
                return True
        return False

    def get_detection_history(self) -> List[dict]:
        """Get detection history for debugging."""
        return self._history.get_history()

    def startup_scan(self) -> List[StateChange]:
        """Perform startup scan and return state changes for running apps."""
        results = self.check_all()
        changes: List[StateChange] = []
        now = datetime.now()

        for app_name, status in results.items():
            if status.is_running:
                open_change = StateChange(
                    app_name=app_name,
                    change_type='opened',
                    timestamp=now,
                    status=status,
                    late_detection=True
                )
                self._history.add(open_change)
                changes.append(open_change)

                self._state_tracker._previous_state[app_name] = status

                if status.has_active_session:
                    session_change = StateChange(
                        app_name=app_name,
                        change_type='session_started',
                        timestamp=now,
                        status=status,
                        late_detection=True
                    )
                    self._history.add(session_change)
                    changes.append(session_change)

        return changes


# For standalone testing
if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG)
    
    monitor = RemoteAccessMonitor()
    monitor.start_realtime_monitoring()
    
    print("\n" + "=" * 60)
    print("REMOTE ACCESS MONITOR - STANDALONE TEST")
    print("=" * 60)
    
    results = monitor.check_all()
    
    print("\nRESULTS:")
    print("=" * 60)
    
    for app_name, status in results.items():
        print(f"\n{app_name.upper()}:")
        print(f"  Running: {status.is_running}")
        print(f"  Active Session: {status.has_active_session}")
        print(f"  Processes: {status.process_count}")
        print(f"  Connections: {status.connection_count}")
        print(f"  Direction: {status.direction}")
        print(f"  Confidence: {status.confidence}")
        if status.remote_ip:
            print(f"  Remote IP: {status.remote_ip}")
        if status.remote_country:
            print(f"  Country: {status.remote_country}")
    
    print("\nPress Ctrl+C to stop...")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        monitor.stop_realtime_monitoring()
        print("\nStopped.")
