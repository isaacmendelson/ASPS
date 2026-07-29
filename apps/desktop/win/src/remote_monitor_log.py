"""
Remote Monitor — Log Parsing, File Watching, and Session Tracking

Contains:
  - LogParser      : regex-based event parser for AnyDesk/TeamViewer/VNC/CRD logs
  - LogWatcher     : real-time tail of a log file in a background thread
  - SessionState   : mutable state for a single remote-access session
  - SessionTracker : event-driven state machine that updates SessionState
  - HistoryReader  : reads historical session records from disk at startup

Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

import re
import time
import threading
import logging
from collections import deque
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from remote_monitor_config import MonitorConfig
from remote_monitor_models import SessionDirection

logger = logging.getLogger(__name__)


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
    # "Participant joined: 123456789"
    RE_TV_PARTICIPANT = re.compile(
        r'[Pp]articipant\s+joined[:\s]*(\d+)',
        re.IGNORECASE
    )
    # Generic IP in TV logs: "IP: 1.2.3.4" / "address 1.2.3.4" / "from 1.2.3.4"
    RE_TV_IP = re.compile(
        r'(?:IP|address|from)[:\s]+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    # TeamViewer logfile patterns (TeamViewer15_Logfile.log)
    RE_TV_PARTNER_ID = re.compile(
        r'(?:Partner\s*ID|PartnerID)[:\s]+(\d+)',
        re.IGNORECASE
    )
    RE_TV_PEER_IP = re.compile(
        r'Peer\s+IP[:\s]+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    RE_TV_FILE_TRANSFER = re.compile(
        r'FileTransfer[:\s]+(Started|Completed|Failed)',
        re.IGNORECASE
    )
    RE_TV_DISCONNECT = re.compile(
        r'(?:session|connection)\s+(?:ended|closed|terminated|disconnected)',
        re.IGNORECASE
    )
    # Connections_incoming.txt — tab-separated forensics-style format (verified
    # against WiredPulse/TeamViewer_Forensics):
    #   PartnerID \t DisplayName \t StartDate \t EndDate \t LoggedOnUser \t ConnectionType \t ConnectionID
    # Date format: dd-MM-yyyy HH:mm:ss
    RE_TV_CONN_INCOMING = re.compile(
        r'^(\d+)\t'                                          # PartnerID
        r'([^\t]*)\t'                                        # DisplayName (may have spaces)
        r'(\d{2}-\d{2}-\d{4}\s\d{2}:\d{2}:\d{2})\t'         # StartDate: dd-MM-yyyy HH:mm:ss
        r'([^\t]*)\t'                                        # EndDate
        r'([^\t]*)\t'                                        # LoggedOnUser
        r'([^\t]*)\t?'                                       # ConnectionType
        r'(.*)',                                             # ConnectionID (GUID, optional)
        re.MULTILINE
    )

    # ─── Chrome Remote Desktop patterns ───────────────────────────────────────

    RE_CRD_CONNECTION = re.compile(
        r'(?:client\s+connected|incoming\s+connection|session\s+started)',
        re.IGNORECASE
    )
    RE_CRD_DISCONNECT = re.compile(
        r'(?:client\s+disconnected|session\s+ended|connection\s+closed)',
        re.IGNORECASE
    )

    # ─── VNC patterns ─────────────────────────────────────────────────────────

    # TigerVNC: "Connections: Accepted: [IP]::port" (IPv4 or bracketed IPv6)
    RE_VNC_ACCEPT = re.compile(
        r'Connections:\s+Accepted:\s+(\[?[\d:a-fA-F\.]+\]?)::(\d+)',
        re.IGNORECASE
    )
    # TigerVNC: "Connections: Closed: [IP]::port"
    RE_VNC_CLOSE = re.compile(
        r'Connections:\s+Closed:\s+(\[?[\d:a-fA-F\.]+\]?)::(\d+)',
        re.IGNORECASE
    )
    # TigerVNC disconnect with reason: "VNCSConnST: Closing [IP]::port: reason"
    RE_VNC_DISCONNECT_REASON = re.compile(
        r'VNCSConnST:\s+Closing\s+(\[?[\d:a-fA-F\.]+\]?)::(\d+):\s+(.+)',
        re.IGNORECASE
    )
    # TigerVNC auth type: "SConnection: Client requests security type VncAuth(2)"
    RE_VNC_AUTH_TYPE = re.compile(
        r'SConnection:\s+Client\s+requests\s+security\s+type\s+(\w+)\((\d+)\)',
        re.IGNORECASE
    )
    # x11vnc: "Got connection from client 192.168.1.100"
    RE_VNC_X11_ACCEPT = re.compile(
        r'Got\s+connection\s+from\s+client\s+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    # Windows VNC (TightVNC/UltraVNC/RealVNC): "Accepted connection from 1.2.3.4"
    RE_VNC_WIN_ACCEPT = re.compile(
        r'(?:Accepted|New)\s+connection\s+from\s+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    # TigerVNC timestamp on its own line: "Sat Feb 28 16:38:09 2026"
    RE_VNC_TIMESTAMP = re.compile(
        r'^(\w{3}\s+\w{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\d{4})\s*$'
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

        # Connections_incoming.txt — tab-separated record of an entire ended
        # session. Match BEFORE the other TV patterns because a digit-leading
        # tab-separated line is unambiguous.
        m = self.RE_TV_CONN_INCOMING.match(line)
        if m:
            try:
                start_ts = datetime.strptime(m.group(3).strip(), "%d-%m-%Y %H:%M:%S")
            except (ValueError, IndexError):
                start_ts = ts
            try:
                end_ts = datetime.strptime(m.group(4).strip(), "%d-%m-%Y %H:%M:%S")
            except (ValueError, IndexError):
                end_ts = None
            return {
                "event": "tv_session_record",
                "timestamp": start_ts,
                "end_time": end_ts,
                "remote_id": m.group(1).strip(),
                "remote_name": m.group(2).strip() if m.group(2) else "",
                "logged_user": m.group(5).strip() if m.group(5) else "",
                "conn_type": m.group(6).strip() if m.group(6) else "",
                "connection_id": m.group(7).strip() if m.group(7) else "",
                "software": "TeamViewer",
                "raw": line.strip(),
            }

        # TeamViewer incoming: "Incoming connection from Partner ID: 123456789"
        m = self.RE_TV_INCOMING.search(line)
        if m:
            ip_match = self.RE_TV_PEER_IP.search(line) or self.RE_TV_IP.search(line)
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": m.group(1) or "",
                "remote_ip": ip_match.group(1) if ip_match else "",
                "raw": line.strip(),
            }

        # TeamViewer "Participant joined: 123456789"
        m = self.RE_TV_PARTICIPANT.search(line)
        if m:
            return {
                "event": "session_started",
                "timestamp": ts,
                "remote_id": m.group(1) or "",
                "raw": line.strip(),
            }

        # TeamViewer file transfer (separate event so we can track activity)
        m = self.RE_TV_FILE_TRANSFER.search(line)
        if m:
            phase = (m.group(1) or "").lower()
            if phase in ("started",):
                return {"event": "file_transfer_start", "timestamp": ts, "raw": line.strip()}
            return {"event": "file_transfer_stop", "timestamp": ts, "raw": line.strip()}

        # TeamViewer Peer IP / generic IP — emit as client_ip for any line that
        # mentions an IP without an incoming-request marker, so the SessionTracker
        # can attach it to the current session.
        m = self.RE_TV_PEER_IP.search(line)
        if m:
            return {
                "event": "client_ip",
                "timestamp": ts,
                "remote_ip": m.group(1),
                "raw": line.strip(),
            }

        # TeamViewer Partner ID without "incoming" keyword — best-effort remote_id capture
        m = self.RE_TV_PARTNER_ID.search(line)
        if m:
            return {
                "event": "remote_info",
                "timestamp": ts,
                "remote_id": m.group(1),
                "raw": line.strip(),
            }

        # TeamViewer disconnect
        if self.RE_TV_DISCONNECT.search(line):
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}

        # ─── VNC patterns ─────────────────────────────────────────────────────

        # TigerVNC: "Connections: Accepted: [IP]::port"
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

        # TigerVNC: "Connections: Closed: ..." (matches both raw close and the
        # disconnect-with-reason variant; reason is logged but not yet routed)
        m = self.RE_VNC_DISCONNECT_REASON.search(line)
        if m:
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}

        m = self.RE_VNC_CLOSE.search(line)
        if m:
            return {"event": "session_stopped", "timestamp": ts, "raw": line.strip()}

        # x11vnc: "Got connection from client 1.2.3.4"
        m = self.RE_VNC_X11_ACCEPT.search(line)
        if m:
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": "",
                "remote_ip": m.group(1),
                "raw": line.strip(),
            }

        # Windows VNC (TightVNC/UltraVNC/RealVNC): "Accepted connection from 1.2.3.4"
        m = self.RE_VNC_WIN_ACCEPT.search(line)
        if m:
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": "",
                "remote_ip": m.group(1),
                "raw": line.strip(),
            }

        # ─── Chrome Remote Desktop patterns ───────────────────────────────────

        if self.RE_CRD_CONNECTION.search(line):
            ip_match = re.search(r'(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})', line)
            return {
                "event": "incoming_request",
                "timestamp": ts,
                "remote_id": "",
                "remote_ip": ip_match.group(1) if ip_match else "",
                "raw": line.strip(),
            }

        if self.RE_CRD_DISCONNECT.search(line):
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

    def read_tail_lines(self, max_lines: int = 500, max_bytes: int = 1_000_000) -> List[str]:
        """
        Read up to `max_lines` lines from the END of the file (bounded by
        `max_bytes`). Used at attach-time to backfill SessionTracker from the
        recent past — solves "session already active before agent restart".
        Does NOT modify self._pos (the tail loop still starts from EOF).
        """
        if not self.path.exists():
            return []
        try:
            size = self.path.stat().st_size
            start = max(0, size - max_bytes)
            with open(self.path, "rb") as f:
                f.seek(start)
                tail_bytes = f.read()
            text = tail_bytes.decode("utf-8", errors="replace")
            # If we cut mid-line, drop the partial first line
            lines = text.splitlines()
            if start > 0 and lines:
                lines = lines[1:]
            lines = [ln for ln in lines if ln.strip()]
            return lines[-max_lines:] if max_lines > 0 else lines
        except (IOError, PermissionError, OSError) as e:
            logger.debug(f"LogWatcher read_tail_lines failed for {self.path}: {e}")
            return []

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
        self.remote_name: str = ""           # display name / hostname (TV)
        self.remote_os: str = ""
        self.remote_version: str = ""
        self.connection_type: str = ""       # "direct" / "relay" / TV ConnectionType
        self.file_transfer_active: bool = False
        self.file_transfers: int = 0
        self.start_time: Optional[datetime] = None
        self.end_time: Optional[datetime] = None
        self.active: bool = False
        self.geoip: dict = {}
        # Forensics fields (TeamViewer Connections_incoming.txt)
        self.logged_user: str = ""           # local user logged on at session time
        self.connection_id: str = ""         # GUID of the session record
        self.software: str = ""              # "AnyDesk" / "TeamViewer" / "VNC" / "ChromeRD"


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

            elif etype == "tv_session_record":
                return self._record_tv_session(event)

        return None

    def _start_incoming(self, event: dict) -> str:
        sess = SessionState()
        sess.direction = SessionDirection.INCOMING
        sess.remote_id = event.get("remote_id", "")
        sess.remote_ip = event.get("remote_ip", "") or self._pending_ip
        self._pending_ip = ""
        sess.start_time = event.get("timestamp") or datetime.now()
        sess.active = True

        # GeoIP lookup — import here to avoid circular imports at module level
        from remote_monitor_geoip import GeoIPLookup
        if sess.remote_ip:
            sess.geoip = GeoIPLookup.lookup(sess.remote_ip)

        self.current = sess
        return "session_started"

    def _start_outgoing(self, event: dict) -> Optional[str]:
        # "Connecting to XXXXX" also appears during relay setup in INCOMING sessions.
        # If we already have an active incoming session, protect it — don't replace
        # it with an outgoing one from a relay line.
        if self.current and self.current.active and self.current.direction == SessionDirection.INCOMING:
            logger.debug(
                f"[REMOTE-MONITOR][PARSER] outgoing_start suppressed — incoming session already active "
                f"(remote_id={self.current.remote_id!r})"
            )
            return None
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
        from remote_monitor_geoip import GeoIPLookup
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

    def _session_stopped(self, event: dict) -> Optional[str]:
        if not self.current:
            return None

        # Guard against stale stop events from a different log stream during
        # backfill: if the stop's timestamp is OLDER than the current session's
        # start, this stop belongs to a previous (already-finished) session and
        # must not clobber the live one.
        evt_ts = event.get("timestamp")
        cur_start = self.current.start_time
        if (
            isinstance(evt_ts, datetime)
            and isinstance(cur_start, datetime)
            and evt_ts < cur_start
        ):
            return None

        self.current.end_time = evt_ts or datetime.now()
        self.current.active = False
        self.history.append(self.current)
        self.current = None
        return "session_ended"

    def _record_tv_session(self, event: dict) -> str:
        """
        Record a fully-finished TeamViewer session from Connections_incoming.txt.
        The line represents a session that has already ended, so we don't touch
        self.current — we just append a populated SessionState to history.
        """
        sess = SessionState()
        sess.direction       = SessionDirection.INCOMING
        sess.software        = event.get("software", "TeamViewer")
        sess.remote_id       = event.get("remote_id", "")
        sess.remote_name     = event.get("remote_name", "")
        sess.logged_user     = event.get("logged_user", "")
        sess.connection_type = event.get("conn_type", "")
        sess.connection_id   = event.get("connection_id", "")
        sess.start_time      = event.get("timestamp")
        sess.end_time        = event.get("end_time")
        sess.active          = False
        self.history.append(sess)
        return "session_ended"

    def get_current_session(self) -> Optional[SessionState]:
        with self._lock:
            return self.current

    def has_active_session(self) -> bool:
        with self._lock:
            return self.current is not None and self.current.active


# ══════════════════════════════════════════════════════════════════════════════
# HISTORY READER — read past sessions from log/trace files at startup
# ══════════════════════════════════════════════════════════════════════════════

class HistoryReader:
    """
    Reads historical session records from disk. Used at agent startup to
    surface sessions that completed while the agent was not running ("late
    detection"). Does NOT emit StateChange events — callers decide what to do
    with the records.

    Sources read:
      - AnyDesk:    %APPDATA%\\AnyDesk\\connection_trace.txt   (one line per session)
      - AnyDesk:    %PROGRAMDATA%\\AnyDesk\\ad_svc.trace        (full event log)
      - TeamViewer: %APPDATA%\\TeamViewer\\Connections_incoming.txt (tab-separated)
    """

    def __init__(self):
        self._parser = LogParser()

    def read_anydesk_connection_trace(self, limit: int = 50, since: Optional[datetime] = None) -> List[dict]:
        """
        Parse `connection_trace.txt` — one line per session, format like:
            "Incoming 2026-02-28, 15:28 User 1458399339"
        Returns dicts with: software, direction, timestamp, remote_id, remote_ip, raw.
        """
        entries: List[dict] = []
        path = MonitorConfig.CONN_TRACE
        if not path.exists():
            return entries

        try:
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                for line in f:
                    line = line.strip()
                    if not line:
                        continue

                    direction = None
                    m = LogParser.RE_CONN_TRACE_IN.search(line)
                    if m:
                        direction = SessionDirection.INCOMING
                    else:
                        m = LogParser.RE_CONN_TRACE_OUT.search(line)
                        if m:
                            direction = SessionDirection.OUTGOING

                    if not (m and direction):
                        continue

                    date_str, time_str, remote_id = m.group(1), m.group(2), m.group(3)
                    try:
                        ts = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H:%M")
                    except ValueError:
                        ts = None

                    if since and ts and ts < since:
                        continue

                    entries.append({
                        "software":  "AnyDesk",
                        "direction": direction,
                        "timestamp": ts,
                        "remote_id": remote_id,
                        "remote_ip": "",
                        "raw":       line,
                    })
        except (IOError, PermissionError) as e:
            logger.debug(f"HistoryReader cannot read {path}: {e}")

        return entries[-limit:] if limit > 0 else entries

    def read_teamviewer_connections(self, limit: int = 50, since: Optional[datetime] = None) -> List[dict]:
        """
        Parse TeamViewer `Connections_incoming.txt` — tab-separated, format:
            PartnerID \\t DisplayName \\t StartDate \\t EndDate \\t LoggedOnUser \\t ConnectionType \\t ConnectionID
            (StartDate/EndDate as dd-MM-yyyy HH:mm:ss)
        Returns dicts with all fields normalized.
        """
        entries: List[dict] = []
        path = MonitorConfig.TV_CONNECTIONS_IN
        if not path.exists():
            return entries

        try:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                for line in f:
                    line = line.rstrip("\n")
                    if not line.strip() or line.startswith("#"):
                        continue

                    parts = line.split("\t")
                    if len(parts) < 4:
                        continue

                    partner_id   = parts[0].strip()
                    partner_name = parts[1].strip() if len(parts) > 1 else ""
                    start_str    = parts[2].strip() if len(parts) > 2 else ""
                    end_str      = parts[3].strip() if len(parts) > 3 else ""
                    logged_user  = parts[4].strip() if len(parts) > 4 else ""
                    conn_type    = parts[5].strip() if len(parts) > 5 else ""
                    conn_id      = parts[6].strip() if len(parts) > 6 else ""

                    ts_start: Optional[datetime] = None
                    ts_end:   Optional[datetime] = None
                    for fmt in ("%d-%m-%Y %H:%M:%S", "%Y-%m-%d %H:%M:%S"):
                        if start_str and ts_start is None:
                            try:
                                ts_start = datetime.strptime(start_str, fmt)
                            except ValueError:
                                pass
                        if end_str and ts_end is None:
                            try:
                                ts_end = datetime.strptime(end_str, fmt)
                            except ValueError:
                                pass

                    if since and ts_start and ts_start < since:
                        continue

                    entries.append({
                        "software":      "TeamViewer",
                        "direction":     SessionDirection.INCOMING,
                        "timestamp":     ts_start,
                        "end_time":      ts_end,
                        "remote_id":     partner_id,
                        "remote_name":   partner_name,
                        "logged_user":   logged_user,
                        "conn_type":     conn_type,
                        "connection_id": conn_id,
                        "raw":           line,
                    })
        except (IOError, PermissionError) as e:
            logger.debug(f"HistoryReader cannot read {path}: {e}")

        return entries[-limit:] if limit > 0 else entries

    def read_anydesk_svc_trace(self, limit: int = 100, since: Optional[datetime] = None) -> List[dict]:
        """
        Parse recent events from `ad_svc.trace`. Returns events as parsed by
        LogParser.parse_line — useful for richer detail (file_transfer, OS info,
        connection_type) than connection_trace.txt provides.
        """
        entries: List[dict] = []
        path = MonitorConfig.SVC_TRACE
        if not path.exists():
            return entries

        try:
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                for line in f:
                    ev = self._parser.parse_line(line)
                    if not ev:
                        continue
                    ts = ev.get("timestamp")
                    if since and ts and ts < since:
                        continue
                    ev["software"] = "AnyDesk"
                    entries.append(ev)
        except (IOError, PermissionError) as e:
            logger.debug(f"HistoryReader cannot read {path}: {e}")

        return entries[-limit:] if limit > 0 else entries

    def read_all_recent(self, hours: int = 24, limit_per_source: int = 50) -> List[dict]:
        """
        Convenience: returns a unified list of session-record-style entries
        from all sources from the last N hours, sorted newest-first.
        """
        cutoff = datetime.now() - timedelta(hours=hours)
        records: List[dict] = []
        records.extend(self.read_anydesk_connection_trace(limit_per_source, since=cutoff))
        records.extend(self.read_teamviewer_connections(limit_per_source, since=cutoff))
        records.sort(
            key=lambda r: r.get("timestamp") or datetime.min,
            reverse=True
        )
        return records
