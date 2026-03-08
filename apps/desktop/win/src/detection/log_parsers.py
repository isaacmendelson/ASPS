"""
Log parsers for tool-specific session detection.
Enhanced with real-time patterns from anydesk_monitor.py

Parses log files from remote access tools to determine:
- Whether a session is currently active
- Connection direction (incoming/outgoing)
- Remote IP address
- Remote OS and version
- File transfer activity
- Connection type (direct/relay)
"""

import os
import re
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Any
from pathlib import Path

logger = logging.getLogger(__name__)


# ══════════════════════════════════════════════════════════════════════════════
# ENHANCED REGEX PATTERNS
# ══════════════════════════════════════════════════════════════════════════════

class LogPatterns:
    """Centralized regex patterns for log parsing."""
    
    # Timestamp
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
    RE_FILE_MANAGER = re.compile(
        r'file\s*manager.*permissions.*\((\w+)',
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
    
    # Relay IP
    RE_RELAY_IP = re.compile(
        r'\((\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\)',
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
    
    # Session active indicators
    SESSION_ACTIVE_KEYWORDS = [
        'desk.connected',
        'incoming connection established',
        'remote control started',
        'session started'
    ]
    
    SESSION_ENDED_KEYWORDS = [
        'desk.disconnected',
        'session closed',
        'remote control stopped',
        'session stopped',
        'session ended'
    ]
    
    # ─── TeamViewer patterns ──────────────────────────────────────────────────
    
    RE_TV_INCOMING = re.compile(
        r'[Ii]ncoming\s+(?:connection|session).*?(?:Partner\s*ID|from)[:\s]*(\d+)',
        re.IGNORECASE
    )
    RE_TV_PARTICIPANT = re.compile(
        r'[Pp]articipant\s+joined[:\s]*(\d+)',
        re.IGNORECASE
    )
    RE_TV_IP = re.compile(
        r'(?:IP|address|from)[:\s]*(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    RE_TV_DISCONNECT = re.compile(
        r'(?:session|connection)\s+(?:ended|closed|terminated|disconnected)',
        re.IGNORECASE
    )
    RE_TV_FILE_TRANSFER = re.compile(
        r'[Ff]ile\s*[Tt]ransfer.*(?:started|completed|received|sent)',
        re.IGNORECASE
    )
    
    # TeamViewer Connections_incoming.txt format (WiredPulse forensics):
    # PartnerID\tDisplayName\tStartDate(dd-MM-yyyy HH:mm:ss)\tEndDate\tLoggedOnUser\tConnectionType\tConnectionID
    RE_TV_CONN_INCOMING = re.compile(
        r'^(\d+)\t'                                          # PartnerID
        r'([^\t]*)\t'                                        # DisplayName
        r'(\d{2}-\d{2}-\d{4}\s\d{2}:\d{2}:\d{2})\t'        # StartDate
        r'([^\t]*)\t'                                        # EndDate
        r'([^\t]*)\t'                                        # LoggedOnUser
        r'([^\t]*)\t?'                                       # ConnectionType
        r'(.*)',                                             # ConnectionID
        re.MULTILINE
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
    RE_VNC_X11_ACCEPT = re.compile(
        r'Got\s+connection\s+from\s+client\s+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )
    RE_VNC_WIN_ACCEPT = re.compile(
        r'(?:Accepted|New)\s+connection\s+from\s+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})',
        re.IGNORECASE
    )


# ══════════════════════════════════════════════════════════════════════════════
# ENHANCED PARSERS
# ══════════════════════════════════════════════════════════════════════════════

def parse_anydesk_trace(log_path: str) -> Dict[str, Any]:
    """
    Parse AnyDesk ad.trace log for session information.
    Enhanced with new patterns for OS, version, file transfer, etc.

    Returns:
        Dict with keys:
        - session_active: bool
        - direction: str - 'incoming', 'outgoing', or 'unknown'
        - remote_ip: Optional[str]
        - remote_id: Optional[str]
        - remote_os: Optional[str]
        - remote_version: Optional[str]
        - connection_type: Optional[str] - 'direct' or 'relay'
        - file_transfer_active: bool
    """
    result = {
        'session_active': False,
        'direction': 'unknown',
        'remote_ip': None,
        'remote_id': None,
        'remote_os': None,
        'remote_version': None,
        'connection_type': None,
        'file_transfer_active': False
    }

    if not os.path.exists(log_path):
        return result

    try:
        # Check if log was modified recently (within 2 minutes)
        mod_time = datetime.fromtimestamp(os.path.getmtime(log_path))
        if datetime.now() - mod_time > timedelta(minutes=2):
            return result

        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()

        # Read last 100 lines for recent activity
        recent_lines = lines[-100:] if len(lines) > 100 else lines

        session_active = False
        file_transfer_active = False

        for line in recent_lines:
            line_lower = line.lower()

            # Session state indicators
            if any(k in line_lower for k in LogPatterns.SESSION_ACTIVE_KEYWORDS):
                session_active = True
            if any(k in line_lower for k in LogPatterns.SESSION_ENDED_KEYWORDS):
                session_active = False

            # Direction detection
            if 'accept request from' in line_lower or LogPatterns.RE_INCOMING.search(line):
                result['direction'] = 'incoming'
            elif 'connecting to' in line_lower or LogPatterns.RE_OUTGOING.search(line):
                result['direction'] = 'outgoing'
            
            # Incoming request with ID
            m = LogPatterns.RE_INCOMING.search(line)
            if m:
                result['remote_id'] = m.group(1)
            
            # IP extraction - new pattern: [IP:port] Incoming connection
            m = LogPatterns.RE_INCOMING_IP.search(line)
            if m:
                result['remote_ip'] = m.group(1)
            
            # IP extraction - old pattern: Client connected from IP
            m = LogPatterns.RE_CLIENT_IP.search(line)
            if m:
                result['remote_ip'] = m.group(1)

            # Legacy IP patterns
            ip_match = re.search(r'logged in from (\d+\.\d+\.\d+\.\d+)', line, re.IGNORECASE)
            if ip_match:
                result['remote_ip'] = ip_match.group(1)

            ext_match = re.search(r'external address[:\s]+(\d+\.\d+\.\d+\.\d+)', line, re.IGNORECASE)
            if ext_match:
                result['remote_ip'] = ext_match.group(1)
            
            # Remote OS
            m = LogPatterns.RE_REMOTE_OS.search(line)
            if m:
                result['remote_os'] = m.group(1)
            
            # Remote version
            m = LogPatterns.RE_REMOTE_VERSION.search(line)
            if m:
                result['remote_version'] = m.group(1)
            
            # Connection type
            if LogPatterns.RE_CONN_TYPE_DIRECT.search(line):
                result['connection_type'] = 'direct'
            elif LogPatterns.RE_CONN_TYPE_RELAY.search(line):
                result['connection_type'] = 'relay'
            
            # File transfer
            if LogPatterns.RE_FILE_TRANSFER_START.search(line):
                file_transfer_active = True
            if LogPatterns.RE_FILE_TRANSFER_STOP.search(line):
                file_transfer_active = False

        result['session_active'] = session_active
        result['file_transfer_active'] = file_transfer_active

    except Exception as e:
        logger.debug(f"Error parsing AnyDesk trace log: {e}")

    return result


def parse_anydesk_svc_trace(log_path: str) -> Dict[str, Any]:
    """
    Parse AnyDesk ad_svc.trace (service log) for session information.
    This log often has more detailed session info.

    Returns same structure as parse_anydesk_trace.
    """
    result = {
        'session_active': False,
        'direction': 'unknown',
        'remote_ip': None,
        'remote_id': None,
        'remote_os': None,
        'remote_version': None,
        'connection_type': None,
        'file_transfer_active': False
    }

    if not os.path.exists(log_path):
        return result

    try:
        mod_time = datetime.fromtimestamp(os.path.getmtime(log_path))
        if datetime.now() - mod_time > timedelta(minutes=2):
            return result

        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()

        recent_lines = lines[-150:] if len(lines) > 150 else lines

        session_active = False

        for line in recent_lines:
            line_lower = line.lower()

            # Session request
            m = LogPatterns.RE_INCOMING.search(line)
            if m:
                result['direction'] = 'incoming'
                result['remote_id'] = m.group(1)
                session_active = True
            
            # Session started
            if LogPatterns.RE_STARTED.search(line):
                session_active = True
            
            # Session stopped
            if LogPatterns.RE_STOPPED.search(line):
                session_active = False
            
            # IP from incoming connection
            m = LogPatterns.RE_INCOMING_IP.search(line)
            if m:
                result['remote_ip'] = m.group(1)
            
            # Client IP (older format)
            m = LogPatterns.RE_CLIENT_IP.search(line)
            if m:
                result['remote_ip'] = m.group(1)

        result['session_active'] = session_active

    except Exception as e:
        logger.debug(f"Error parsing AnyDesk svc trace: {e}")

    return result


def parse_anydesk_connection_trace(log_path: str) -> List[Dict[str, Any]]:
    """
    Parse AnyDesk connection_trace.txt for connection history.
    Enhanced format: "Incoming YYYY-MM-DD, HH:mm User <ID> <ID>"

    Returns:
        List of connection dicts with:
        - date: str
        - time: str
        - remote_id: str
        - direction: str
    """
    connections = []

    if not os.path.exists(log_path):
        return connections

    try:
        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()

        now = datetime.now()
        five_minutes_ago = now - timedelta(minutes=5)

        for line in lines:
            # Try incoming pattern
            m = LogPatterns.RE_CONN_TRACE_IN.search(line)
            if m:
                date_str, time_str, remote_id = m.group(1), m.group(2), m.group(3)
                try:
                    conn_dt = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H:%M")
                    if conn_dt >= five_minutes_ago:
                        connections.append({
                            'date': date_str,
                            'time': time_str,
                            'remote_id': remote_id,
                            'direction': 'incoming'
                        })
                except ValueError:
                    continue
            
            # Try outgoing pattern
            m = LogPatterns.RE_CONN_TRACE_OUT.search(line)
            if m:
                date_str, time_str, remote_id = m.group(1), m.group(2), m.group(3)
                try:
                    conn_dt = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H:%M")
                    if conn_dt >= five_minutes_ago:
                        connections.append({
                            'date': date_str,
                            'time': time_str,
                            'remote_id': remote_id,
                            'direction': 'outgoing'
                        })
                except ValueError:
                    continue

            # Also try legacy pattern
            pattern = re.compile(
                r'(Incoming|Outgoing)\s+'
                r'(\d{4}-\d{2}-\d{2}),\s+'
                r'(\d{2}:\d{2})\s+'
                r'\[([^\]]*)\]\s+'
                r'<(\d+)>'
            )
            match = pattern.search(line)
            if match:
                direction = match.group(1).lower()
                date_str = match.group(2)
                time_str = match.group(3)
                remote_id = match.group(5)

                try:
                    conn_dt = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H:%M")
                    if conn_dt >= five_minutes_ago:
                        connections.append({
                            'date': date_str,
                            'time': time_str,
                            'remote_id': remote_id,
                            'direction': direction
                        })
                except ValueError:
                    continue

    except Exception as e:
        logger.debug(f"Error parsing AnyDesk connection trace: {e}")

    return connections


def parse_teamviewer_log(log_path: str) -> Dict[str, Any]:
    """
    Parse TeamViewer log file for session information.
    Enhanced with more patterns.
    """
    result = {
        'session_active': False,
        'direction': 'unknown',
        'remote_ip': None,
        'remote_id': None,
        'file_transfer_active': False
    }

    if not os.path.exists(log_path):
        return result

    try:
        mod_time = datetime.fromtimestamp(os.path.getmtime(log_path))
        if datetime.now() - mod_time > timedelta(minutes=2):
            return result

        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()

        recent_lines = lines[-100:] if len(lines) > 100 else lines

        session_active = False

        for line in recent_lines:
            line_lower = line.lower()

            # Session state
            if 'connection established' in line_lower or 'addparticipant' in line_lower:
                session_active = True
            if LogPatterns.RE_TV_DISCONNECT.search(line):
                session_active = False

            # Incoming detection
            m = LogPatterns.RE_TV_INCOMING.search(line)
            if m:
                result['direction'] = 'incoming'
                result['remote_id'] = m.group(1)

            # IP extraction
            m = LogPatterns.RE_TV_IP.search(line)
            if m and 'connection' in line_lower:
                result['remote_ip'] = m.group(1)
            
            # File transfer
            if LogPatterns.RE_TV_FILE_TRANSFER.search(line):
                result['file_transfer_active'] = True

        result['session_active'] = session_active

    except Exception as e:
        logger.debug(f"Error parsing TeamViewer log: {e}")

    return result


def parse_teamviewer_connections_incoming(log_path: str) -> List[Dict[str, Any]]:
    """
    Parse TeamViewer Connections_incoming.txt for connection history.
    Format: Tab-separated (WiredPulse forensics format)
    """
    connections = []

    if not os.path.exists(log_path):
        return connections

    try:
        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                
                parts = line.split("\t")
                if len(parts) >= 4:
                    try:
                        partner_id = parts[0].strip()
                        partner_name = parts[1].strip() if len(parts) > 1 else ""
                        start_str = parts[2].strip() if len(parts) > 2 else ""
                        end_str = parts[3].strip() if len(parts) > 3 else ""
                        
                        # Parse timestamp - dd-MM-yyyy HH:mm:ss
                        ts = None
                        if start_str:
                            try:
                                ts = datetime.strptime(start_str, "%d-%m-%Y %H:%M:%S")
                            except ValueError:
                                try:
                                    ts = datetime.strptime(start_str, "%Y-%m-%d %H:%M:%S")
                                except ValueError:
                                    pass
                        
                        # Only include recent connections
                        if ts and datetime.now() - ts < timedelta(minutes=10):
                            connections.append({
                                'remote_id': partner_id,
                                'remote_name': partner_name,
                                'timestamp': ts,
                                'direction': 'incoming'
                            })
                    except Exception:
                        pass
    except Exception as e:
        logger.debug(f"Error parsing TeamViewer connections: {e}")

    return connections


def parse_tool_logs(app_name: str, tool_config: Dict[str, Any]) -> Dict[str, Any]:
    """
    Dispatcher function that calls appropriate parser based on app name.
    Enhanced to return more detailed information.

    Returns:
        Combined signals dict:
        - log_session_active: bool
        - log_direction: Optional[str]
        - log_remote_ip: Optional[str]
        - log_remote_id: Optional[str]
        - log_remote_os: Optional[str]
        - log_remote_version: Optional[str]
        - log_connection_type: Optional[str]
        - log_file_transfer_active: bool
    """
    result = {
        'log_session_active': False,
        'log_direction': None,
        'log_remote_ip': None,
        'log_remote_id': None,
        'log_remote_os': None,
        'log_remote_version': None,
        'log_connection_type': None,
        'log_file_transfer_active': False
    }

    if not tool_config:
        return result

    log_paths = tool_config.get('log_paths', {})
    if not log_paths:
        return result

    try:
        if app_name == 'anydesk':
            # Parse main trace log
            trace_path = log_paths.get('trace')
            if trace_path:
                trace_result = parse_anydesk_trace(trace_path)
                result['log_session_active'] = trace_result['session_active']
                result['log_direction'] = trace_result['direction'] if trace_result['direction'] != 'unknown' else None
                result['log_remote_ip'] = trace_result['remote_ip']
                result['log_remote_id'] = trace_result.get('remote_id')
                result['log_remote_os'] = trace_result.get('remote_os')
                result['log_remote_version'] = trace_result.get('remote_version')
                result['log_connection_type'] = trace_result.get('connection_type')
                result['log_file_transfer_active'] = trace_result.get('file_transfer_active', False)
            
            # Also parse service trace for more info
            svc_trace_path = log_paths.get('svc_trace')
            if svc_trace_path:
                svc_result = parse_anydesk_svc_trace(svc_trace_path)
                # Merge results - prefer values from svc trace if present
                if svc_result['session_active']:
                    result['log_session_active'] = True
                if svc_result['remote_ip'] and not result['log_remote_ip']:
                    result['log_remote_ip'] = svc_result['remote_ip']
                if svc_result['remote_id'] and not result['log_remote_id']:
                    result['log_remote_id'] = svc_result['remote_id']
                if svc_result['direction'] != 'unknown' and not result['log_direction']:
                    result['log_direction'] = svc_result['direction']

            # Check connection trace for recent activity
            conn_trace_path = log_paths.get('connection_trace')
            if conn_trace_path:
                recent_connections = parse_anydesk_connection_trace(conn_trace_path)
                if recent_connections:
                    result['log_session_active'] = True
                    result['log_direction'] = recent_connections[-1]['direction']
                    if not result['log_remote_id']:
                        result['log_remote_id'] = recent_connections[-1]['remote_id']

        elif app_name == 'teamviewer':
            # Parse main log file
            logfile_path = log_paths.get('logfile')
            if logfile_path:
                tv_result = parse_teamviewer_log(logfile_path)
                result['log_session_active'] = tv_result['session_active']
                result['log_direction'] = tv_result['direction'] if tv_result['direction'] != 'unknown' else None
                result['log_remote_ip'] = tv_result['remote_ip']
                result['log_remote_id'] = tv_result.get('remote_id')
                result['log_file_transfer_active'] = tv_result.get('file_transfer_active', False)

            # Check incoming connections file
            incoming_path = log_paths.get('connections_incoming')
            if incoming_path:
                recent_connections = parse_teamviewer_connections_incoming(incoming_path)
                if recent_connections:
                    result['log_session_active'] = True
                    result['log_direction'] = 'incoming'
                    if not result['log_remote_id']:
                        result['log_remote_id'] = recent_connections[-1]['remote_id']

    except Exception as e:
        logger.debug(f"Error in parse_tool_logs for {app_name}: {e}")

    return result
