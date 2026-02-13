"""
Log parsers for tool-specific session detection.

Parses log files from remote access tools to determine:
- Whether a session is currently active
- Connection direction (incoming/outgoing)
- Remote IP address
"""

import os
import re
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Any

logger = logging.getLogger(__name__)


def parse_anydesk_trace(log_path: str) -> Dict[str, Any]:
    """
    Parse AnyDesk ad.trace log for session information.

    Args:
        log_path: Path to ad.trace file

    Returns:
        Dict with keys:
        - session_active: bool - Whether a session is currently active
        - direction: str - 'incoming', 'outgoing', or 'unknown'
        - remote_ip: Optional[str] - Remote IP address if detected
    """
    result = {
        'session_active': False,
        'direction': 'unknown',
        'remote_ip': None
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

        for line in recent_lines:
            line_lower = line.lower()

            # Session state indicators
            if any(k in line_lower for k in ['desk.connected', 'incoming connection established', 'remote control started']):
                session_active = True
            if any(k in line_lower for k in ['desk.disconnected', 'session closed', 'remote control stopped']):
                session_active = False

            # Direction detection
            if 'accept request from' in line_lower:
                result['direction'] = 'incoming'
            elif 'connecting to' in line_lower:
                result['direction'] = 'outgoing'

            # IP extraction patterns
            # "logged in from X.X.X.X"
            ip_match = re.search(r'logged in from (\d+\.\d+\.\d+\.\d+)', line, re.IGNORECASE)
            if ip_match:
                result['remote_ip'] = ip_match.group(1)

            # "External address: X.X.X.X"
            ext_match = re.search(r'external address[:\s]+(\d+\.\d+\.\d+\.\d+)', line, re.IGNORECASE)
            if ext_match:
                result['remote_ip'] = ext_match.group(1)

        result['session_active'] = session_active

    except Exception as e:
        logger.debug(f"Error parsing AnyDesk trace log: {e}")

    return result


def parse_anydesk_connection_trace(log_path: str) -> List[Dict[str, Any]]:
    """
    Parse AnyDesk connection_trace.txt for connection history.

    Format: "Incoming YYYY-MM-DD, HH:mm [Username] <9-digit-ID>"

    Args:
        log_path: Path to connection_trace.txt file

    Returns:
        List of connection dicts with:
        - date: str - Connection date
        - time: str - Connection time
        - username: Optional[str] - Connected username
        - anydesk_id: str - 9-digit AnyDesk ID
        - direction: str - 'incoming' or 'outgoing'
    """
    connections = []

    if not os.path.exists(log_path):
        return connections

    try:
        with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()

        # Pattern: Incoming|Outgoing YYYY-MM-DD, HH:mm [Username] <ID>
        pattern = re.compile(
            r'(Incoming|Outgoing)\s+'
            r'(\d{4}-\d{2}-\d{2}),\s+'
            r'(\d{2}:\d{2})\s+'
            r'\[([^\]]*)\]\s+'
            r'<(\d+)>'
        )

        now = datetime.now()
        five_minutes_ago = now - timedelta(minutes=5)

        for line in lines:
            match = pattern.search(line)
            if match:
                direction = match.group(1).lower()
                date_str = match.group(2)
                time_str = match.group(3)
                username = match.group(4) or None
                anydesk_id = match.group(5)

                # Parse datetime to filter recent connections
                try:
                    conn_dt = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H:%M")
                    if conn_dt >= five_minutes_ago:
                        connections.append({
                            'date': date_str,
                            'time': time_str,
                            'username': username,
                            'anydesk_id': anydesk_id,
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

    Args:
        log_path: Path to TeamViewer log file

    Returns:
        Dict with keys:
        - session_active: bool - Whether a session is currently active
        - direction: str - 'incoming', 'outgoing', or 'unknown'
        - remote_ip: Optional[str] - Remote IP address if detected
    """
    result = {
        'session_active': False,
        'direction': 'unknown',
        'remote_ip': None
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

        for line in recent_lines:
            line_lower = line.lower()

            # Session state indicators
            if 'connection established' in line_lower or 'addparticipant' in line_lower:
                session_active = True
            if 'connection closed' in line_lower or 'session ended' in line_lower:
                session_active = False

            # IP extraction
            ip_match = re.search(r'(\d+\.\d+\.\d+\.\d+)', line)
            if ip_match and 'connection' in line_lower:
                result['remote_ip'] = ip_match.group(1)

        result['session_active'] = session_active

    except Exception as e:
        logger.debug(f"Error parsing TeamViewer log: {e}")

    return result


def parse_tool_logs(app_name: str, tool_config: Dict[str, Any]) -> Dict[str, Any]:
    """
    Dispatcher function that calls appropriate parser based on app name.

    Args:
        app_name: Name of the remote access tool
        tool_config: Tool configuration from get_tool_config()

    Returns:
        Combined signals dict:
        - log_session_active: bool - Whether log indicates active session
        - log_direction: Optional[str] - Direction from log ('incoming'/'outgoing')
        - log_remote_ip: Optional[str] - Remote IP from log
    """
    result = {
        'log_session_active': False,
        'log_direction': None,
        'log_remote_ip': None
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

            # Also check connection trace for recent activity
            conn_trace_path = log_paths.get('connection_trace')
            if conn_trace_path:
                recent_connections = parse_anydesk_connection_trace(conn_trace_path)
                if recent_connections:
                    result['log_session_active'] = True
                    # Use direction from most recent connection
                    result['log_direction'] = recent_connections[-1]['direction']

        elif app_name == 'teamviewer':
            # Parse main log file
            logfile_path = log_paths.get('logfile')
            if logfile_path:
                tv_result = parse_teamviewer_log(logfile_path)
                result['log_session_active'] = tv_result['session_active']
                result['log_direction'] = tv_result['direction'] if tv_result['direction'] != 'unknown' else None
                result['log_remote_ip'] = tv_result['remote_ip']

            # Check incoming connections file for direction
            incoming_path = log_paths.get('connections_incoming')
            if incoming_path and os.path.exists(incoming_path):
                try:
                    mod_time = datetime.fromtimestamp(os.path.getmtime(incoming_path))
                    if datetime.now() - mod_time < timedelta(minutes=5):
                        result['log_direction'] = 'incoming'
                except Exception:
                    pass

    except Exception as e:
        logger.debug(f"Error in parse_tool_logs for {app_name}: {e}")

    return result
