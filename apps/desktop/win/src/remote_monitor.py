"""
AntiScam Desktop App - Remote Access Monitor
Monitors AnyDesk, TeamViewer, and other remote access tools
With full debugging output
"""

import psutil
import os
import platform
import time
import json
from collections import deque
from datetime import datetime, timedelta
from typing import Dict, Optional, List, Tuple
from dataclasses import dataclass, field
import logging

from config import REMOTE_APPS, ConnectionStatus, WHITELIST_IPS, WHITELIST_PORTS, DEBUG_MODE
from detection.tools import get_tool_config
from detection.log_parsers import parse_tool_logs
from detection.confidence import calculate_confidence, Confidence
from detection.direction import detect_direction, Direction
from detection.geolocation import get_geolocator

logger = logging.getLogger(__name__)


@dataclass
class RemoteAppStatus:
    """Status of a remote access application"""
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
    remote_country: Optional[str] = None  # Country name from GeoIP
    remote_country_code: Optional[str] = None  # ISO country code (e.g., 'US')


@dataclass
class StateChange:
    """Represents a state change event for a remote access app"""
    app_name: str
    change_type: str  # 'opened', 'closed', 'session_started', 'session_ended'
    timestamp: datetime
    status: RemoteAppStatus
    late_detection: bool = False  # True if detected on startup


class DetectionHistory:
    """Rolling log of detection events for debugging."""

    def __init__(self, max_events: int = 100):
        self._events: deque = deque(maxlen=max_events)

    def add(self, state_change: StateChange):
        """Add event to history."""
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
        """Get all events (newest first)."""
        return list(reversed(self._events))

    def export_for_debug(self) -> str:
        """Export as JSON string for debugging."""
        return json.dumps(self.get_history(), indent=2)


class DebouncedStateTracker:
    """Tracks state changes with debouncing for close events."""

    def __init__(self, close_debounce_seconds: float = 3.0, session_end_debounce_seconds: float = 10.0):
        self._close_debounce = close_debounce_seconds
        self._session_end_debounce = session_end_debounce_seconds
        self._pending_closes: Dict[str, float] = {}  # app_name -> timestamp
        self._pending_session_ends: Dict[str, float] = {}  # app_name -> timestamp
        self._previous_state: Dict[str, RemoteAppStatus] = {}

    def process_state(self, app_name: str, current_status: RemoteAppStatus) -> Optional[StateChange]:
        """
        Process current state and return a StateChange if a transition occurred.
        Returns None if no state change or if change is pending debounce.
        """
        prev = self._previous_state.get(app_name)
        now = datetime.now()

        # App just closed - schedule pending close
        if prev and prev.is_running and not current_status.is_running:
            self._pending_closes[app_name] = time.time()
            self._pending_session_ends.pop(app_name, None)  # Cancel any pending session end
            self._previous_state[app_name] = current_status
            return None  # Don't report yet, wait for debounce

        # App running but was in pending_closes - cancel pending close (quick restart)
        if current_status.is_running and app_name in self._pending_closes:
            del self._pending_closes[app_name]
            self._pending_session_ends.pop(app_name, None)  # Also cancel pending session end
            # No state change to report - it's a quick restart
            self._previous_state[app_name] = current_status
            return None

        # App just opened (not a restart from pending)
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
                # Cancel any pending session_end (session resumed during debounce window)
                self._pending_session_ends.pop(app_name, None)
                self._previous_state[app_name] = current_status
                return StateChange(
                    app_name=app_name,
                    change_type='session_started',
                    timestamp=now,
                    status=current_status
                )
            # Session just ended - schedule pending session end (debounce)
            if not current_status.has_active_session and prev.has_active_session:
                self._pending_session_ends[app_name] = time.time()
                self._previous_state[app_name] = current_status
                return None  # Wait for debounce

        self._previous_state[app_name] = current_status
        return None

    def check_pending_events(self) -> List[StateChange]:
        """
        Check for close and session-end events that have completed their debounce period.
        Returns list of StateChange events.
        """
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
                # Also clean up any pending session end for this app (it's fully closed)
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
    
    
class RemoteAccessMonitor:
    """Monitor for remote access applications"""

    # Ports used for infrastructure (not active sessions)
    INFRASTRUCTURE_PORTS = [80, 443, 6568, 7070]

    def __init__(self):
        self.system = platform.system()
        self._last_status: Dict[str, RemoteAppStatus] = {}
        self._state_tracker = DebouncedStateTracker(close_debounce_seconds=3, session_end_debounce_seconds=10)
        self._history = DetectionHistory(max_events=100)
        print(f"[REMOTE-MONITOR] Initialized on {self.system}")
        
    def get_log_path(self, app_name: str) -> Optional[str]:
        """Get log file path for an app based on OS"""
        app_config = REMOTE_APPS.get(app_name)
        if not app_config:
            return None
            
        if self.system == "Windows":
            path = app_config.get('log_path_windows')
        elif self.system == "Linux":
            path = app_config.get('log_path_linux')
        elif self.system == "Darwin":
            path = app_config.get('log_path_mac')
        else:
            return None
            
        if path:
            return os.path.expanduser(path)
        return None
    
    def check_log_for_session(self, app_name: str) -> bool:
        """Check app log file for active session indicators"""
        log_path = self.get_log_path(app_name)

        if not log_path or not os.path.exists(log_path):
            return False
            
        try:
            # Check if log was modified recently
            mod_time = datetime.fromtimestamp(os.path.getmtime(log_path))
            time_diff = datetime.now() - mod_time

            if time_diff > timedelta(minutes=2):
                return False

            with open(log_path, 'r', encoding='utf-8', errors='ignore') as f:
                lines = f.readlines()
                recent_lines = lines[-50:] if len(lines) > 50 else lines

                session_active = False

                # AnyDesk indicators
                if app_name == 'anydesk':
                    for line in recent_lines:
                        line_lower = line.lower()
                        if any(k in line_lower for k in ['desk.connected', 'incoming connection established', 'remote control started']):
                            session_active = True
                        if any(k in line_lower for k in ['desk.disconnected', 'session closed', 'remote control stopped']):
                            session_active = False

                # TeamViewer indicators
                elif app_name == 'teamviewer':
                    for line in recent_lines:
                        line_lower = line.lower()
                        if 'connection established' in line_lower:
                            session_active = True
                        if 'connection closed' in line_lower or 'session ended' in line_lower:
                            session_active = False

                return session_active

        except Exception as e:
            logger.debug(f"Error reading log for {app_name}: {e}")
            return False
    
    def find_processes(self, app_name: str) -> List[psutil.Process]:
        """Find all processes for a given app"""
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
        """Single process_iter() call -- returns {app_name: [processes]} for all monitored apps."""
        # Build reverse lookup: lowercase process name substring -> list of app names
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

        # Get enhanced tool config for log parsing
        tool_config = get_tool_config(app_name)
        total_conn, suspicious_conn, remote_ip = self.check_suspicious_connections(processes, app_name)
        cpu_usage = self.check_cpu_usage(processes, app_name)

        log_signals = {}
        if tool_config:
            log_signals = parse_tool_logs(app_name, tool_config)
        log_session_active = log_signals.get('log_session_active', False)
        log_remote_ip = log_signals.get('log_remote_ip')

        service_running = False
        if tool_config and tool_config.get('service_names'):
            for svc_name in tool_config['service_names']:
                if self.check_windows_service(svc_name):
                    service_running = True
                    break

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

        connection_info = []
        for proc in processes:
            try:
                for conn in proc.net_connections():
                    if conn.status == 'ESTABLISHED' and conn.raddr:
                        connection_info.append({
                            'local_port': conn.laddr.port if conn.laddr else 0,
                            'remote_port': conn.raddr.port if conn.raddr else 0
                        })
            except (psutil.AccessDenied, psutil.NoSuchProcess):
                continue

        direction_result = detect_direction(connection_info, tool_config, log_signals)
        direction_str = direction_result.value if direction_result else Direction.UNKNOWN.value

        remote_country = None
        remote_country_code = None
        if final_remote_ip:
            geo = get_geolocator()
            country_info = geo.get_country(final_remote_ip)
            remote_country = country_info['country']
            remote_country_code = country_info['country_code']

        status = RemoteAppStatus(
            app_name=app_name,
            app_id=app_config['id'],
            is_running=True,
            has_active_session=has_active_session,
            process_count=len(processes),
            connection_count=total_conn,
            connection_status=conn_status,
            remote_ip=final_remote_ip,
            direction=direction_str,
            confidence=confidence,
            remote_country=remote_country,
            remote_country_code=remote_country_code
        )

        self._last_status[app_name] = status
        return status

    def is_whitelisted(self, ip: str, port: int) -> bool:
        """Check if connection is whitelisted (our servers)"""
        if ip in WHITELIST_IPS:
            return True
        if port in WHITELIST_PORTS:
            return True
        return False
    
    def check_suspicious_connections(self, processes: List[psutil.Process], app_name: str) -> tuple:
        """Check for suspicious network connections"""
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

                        # Skip localhost
                        if ip.startswith('127.') or ip == '::1':
                            continue

                        # Skip whitelisted (our servers)
                        if self.is_whitelisted(ip, port):
                            continue

                        # Skip infrastructure ports
                        if port in self.INFRASTRUCTURE_PORTS:
                            continue

                        suspicious_count += 1
                        remote_ip = ip

            except (psutil.AccessDenied, AttributeError) as e:
                continue

        return total_connections, suspicious_count, remote_ip
    
    def check_cpu_usage(self, processes: List[psutil.Process], app_name: str) -> float:
        """Check total CPU usage of processes"""
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

    def check_app(self, app_name: str) -> RemoteAppStatus:
        """Check status of a specific remote access app using multi-signal detection."""
        app_config = REMOTE_APPS.get(app_name)
        if not app_config:
            return RemoteAppStatus(
                app_name=app_name,
                app_id=0,
                is_running=False,
                has_active_session=False,
                process_count=0,
                connection_count=0,
                connection_status=ConnectionStatus.UNKNOWN
            )

        # Find processes
        processes = self.find_processes(app_name)

        if not processes:
            return RemoteAppStatus(
                app_name=app_name,
                app_id=app_config['id'],
                is_running=False,
                has_active_session=False,
                process_count=0,
                connection_count=0,
                connection_status=ConnectionStatus.CLOSED
            )

        # Get enhanced tool config for log parsing
        tool_config = get_tool_config(app_name)

        # Check connections
        total_conn, suspicious_conn, remote_ip = self.check_suspicious_connections(processes, app_name)

        # Check CPU
        cpu_usage = self.check_cpu_usage(processes, app_name)

        # Check logs using detection submodule
        log_signals = {}
        if tool_config:
            log_signals = parse_tool_logs(app_name, tool_config)

        log_session_active = log_signals.get('log_session_active', False)
        log_direction = log_signals.get('log_direction')
        log_remote_ip = log_signals.get('log_remote_ip')

        # Check Windows service if configured
        service_running = False
        if tool_config and tool_config.get('service_names'):
            for svc_name in tool_config['service_names']:
                if self.check_windows_service(svc_name):
                    service_running = True
                    break

        # Build signals dict for confidence calculation
        signals = {
            'active_connection': suspicious_conn > 0,
            'log_session_active': log_session_active,
            'cpu_active': cpu_usage > 5.0,
            'service_running': service_running
        }

        # Calculate confidence level
        confidence = calculate_confidence(signals)

        # Determine if session is active (at least one signal)
        has_active_session = any([
            signals['active_connection'],
            signals['log_session_active'],
            signals['cpu_active']
        ])

        # Use log-provided IP if no network IP found
        final_remote_ip = remote_ip or log_remote_ip

        # Determine connection status
        if has_active_session:
            conn_status = ConnectionStatus.OPEN
        else:
            conn_status = ConnectionStatus.CLOSED

        # Build connection info for direction detection
        connection_info = []
        for proc in processes:
            try:
                for conn in proc.net_connections():
                    if conn.status == 'ESTABLISHED' and conn.raddr:
                        connection_info.append({
                            'local_port': conn.laddr.port if conn.laddr else 0,
                            'remote_port': conn.raddr.port if conn.raddr else 0
                        })
            except (psutil.AccessDenied, psutil.NoSuchProcess):
                continue

        # Detect direction using combined signals
        direction_result = detect_direction(connection_info, tool_config, log_signals)
        direction_str = direction_result.value if direction_result else Direction.UNKNOWN.value

        # Geolocation lookup for remote IP
        remote_country = None
        remote_country_code = None
        if final_remote_ip:
            geo = get_geolocator()
            country_info = geo.get_country(final_remote_ip)
            remote_country = country_info['country']
            remote_country_code = country_info['country_code']

        status = RemoteAppStatus(
            app_name=app_name,
            app_id=app_config['id'],
            is_running=True,
            has_active_session=has_active_session,
            process_count=len(processes),
            connection_count=total_conn,
            connection_status=conn_status,
            remote_ip=final_remote_ip,
            direction=direction_str,
            confidence=confidence,
            remote_country=remote_country,
            remote_country_code=remote_country_code
        )

        self._last_status[app_name] = status
        return status
    
    def check_all(self) -> Dict[str, RemoteAppStatus]:
        """Check all monitored remote access apps with a single process scan."""
        processes_by_app = self._scan_all_processes()
        results = {}
        for app_name in REMOTE_APPS.keys():
            results[app_name] = self._check_app_with_processes(
                app_name, processes_by_app.get(app_name, [])
            )
        return results
    
    def get_active_sessions(self) -> List[RemoteAppStatus]:
        """Get list of apps with active sessions"""
        all_status = self.check_all()
        return [s for s in all_status.values() if s.has_active_session]
    
    def has_any_active_session(self) -> bool:
        """Quick check if any remote app has active session"""
        for app_name in REMOTE_APPS.keys():
            status = self.check_app(app_name)
            if status.has_active_session:
                return True
        return False

    def check_all_with_changes(self) -> Tuple[Dict[str, RemoteAppStatus], List[StateChange]]:
        """
        Check all monitored remote access apps and return state changes.
        Returns both current status dict and list of state changes.
        """
        results = self.check_all()
        changes: List[StateChange] = []

        # Process each app through state tracker
        for app_name, status in results.items():
            change = self._state_tracker.process_state(app_name, status)
            if change:
                self._history.add(change)
                changes.append(change)

        # Check for debounced events (closes + session ends)
        pending_events = self._state_tracker.check_pending_events()
        for event in pending_events:
            self._history.add(event)
            changes.append(event)

        return results, changes

    def get_detection_history(self) -> List[dict]:
        """Get detection history (newest first) for debugging."""
        return self._history.get_history()

    def startup_scan(self) -> List[StateChange]:
        """
        Perform startup scan and return state changes for currently running apps.
        All detected apps/sessions are marked with late_detection=True.
        """
        results = self.check_all()
        changes: List[StateChange] = []
        now = datetime.now()

        for app_name, status in results.items():
            # If app is running, report as opened (late detection)
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

                # Initialize state tracker with current state
                self._state_tracker._previous_state[app_name] = status

                # If also has active session, report session_started (late detection)
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
    
    if DEBUG_MODE:
        print("\n" + "=" * 60)
    if DEBUG_MODE:
        print("REMOTE ACCESS MONITOR - STANDALONE TEST")
    if DEBUG_MODE:
        print("=" * 60)
    
    results = monitor.check_all()
    
    if DEBUG_MODE:
        print("\n" + "=" * 60)
    if DEBUG_MODE:
        print("FINAL RESULTS:")
    if DEBUG_MODE:
        print("=" * 60)
    
    for app_name, status in results.items():
        if DEBUG_MODE:
            print(f"\n{app_name.upper()}:")
        if DEBUG_MODE:
            print(f"  Running: {status.is_running}")
        if DEBUG_MODE:
            print(f"  Active Session: {status.has_active_session}")
        if DEBUG_MODE:
            print(f"  Processes: {status.process_count}")
        if DEBUG_MODE:
            print(f"  Connections: {status.connection_count}")
        if status.remote_ip:
            if DEBUG_MODE:
                print(f"  Remote IP: {status.remote_ip}")
