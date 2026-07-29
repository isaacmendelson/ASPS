"""
AntiScam Desktop App - Remote Access Monitor
Enhanced detection with real-time log parsing
Author: Tommy the Hacker + ASPS Team

Monitors: AnyDesk, TeamViewer, Chrome Remote Desktop, VNC, and more
Features:
- Real-time log file watching
- Multi-signal detection (process, network, logs, CPU)
- Direction detection (incoming/outgoing)
- GeoIP lookup
- File transfer detection
- Connection type detection (direct/relay)

ASPS-627: This file has been split into focused sub-modules:
  - remote_monitor_config.py    : MonitorConfig, IS_WINDOWS
  - remote_monitor_models.py    : RemoteAppStatus, StateChange, SessionDirection
  - remote_monitor_geoip.py     : GeoIPLookup
  - remote_monitor_log.py       : LogParser, LogWatcher, SessionState, SessionTracker, HistoryReader
  - remote_monitor_detection.py : DetectionHistory, DebouncedStateTracker, calculate_confidence
  - remote_monitor_cli.py       : AnyDeskCLI, TeamViewerCLI

All public names are re-exported from here so existing importers continue to work.
"""

import sys
import threading
import logging
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import psutil

from config import REMOTE_APPS, ConnectionStatus, WHITELIST_IPS, WHITELIST_PORTS, DEBUG_MODE
# REMOVED: parse_tool_logs - now using real-time SessionTracker only
# from detection.tools import get_tool_config
# from detection.log_parsers import parse_tool_logs
from detection.geolocation import get_geolocator

# ── Sub-module imports (also re-exported for backward compatibility) ──────────
from remote_monitor_config import MonitorConfig, IS_WINDOWS
from remote_monitor_models import RemoteAppStatus, StateChange, SessionDirection
from remote_monitor_geoip import GeoIPLookup
from remote_monitor_log import (
    LogParser, LogWatcher, SessionState, SessionTracker, HistoryReader
)
from remote_monitor_detection import (
    DetectionHistory, DebouncedStateTracker, calculate_confidence
)
from remote_monitor_cli import AnyDeskCLI, TeamViewerCLI

logger = logging.getLogger(__name__)


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
        self._state_tracker = DebouncedStateTracker(close_debounce_seconds=1, session_end_debounce_seconds=4)
        self._history = DetectionHistory(max_events=100)

        # Enhanced tracking per app
        self._session_trackers: Dict[str, SessionTracker] = {}
        self._log_watchers: Dict[str, List[LogWatcher]] = {}
        self._log_parser = LogParser()
        self._watcher_threads: List[threading.Thread] = []
        self._running = False

        logger.info(f"RemoteAccessMonitor initialized on {self.system}")

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

        logger.info("Real-time monitoring started for all apps")

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

        # Build watchers (no threads yet)
        logger.debug("AnyDesk log paths at startup:")
        for path in log_paths:
            try:
                exists = path.exists()
                size = path.stat().st_size if exists else 0
                readable = False
                if exists:
                    try:
                        with open(path, "r", encoding="utf-8", errors="replace") as _f:
                            readable = True
                    except (IOError, PermissionError) as e:
                        logger.debug(f"  {path}: not readable: {e}")
                logger.debug(f"  {path}: exists={exists}, size={size}, readable={readable}")
            except Exception as e:
                logger.warning(f"  {path}: ERROR {e}")
            watcher = LogWatcher(path, MonitorConfig.POLL_INTERVAL)
            self._log_watchers[app_name].append(watcher)

        # Unified backfill BEFORE starting tail threads (cross-file timestamp order)
        self._backfill_app(app_name, self._log_watchers[app_name])

        # Start live tail threads
        for watcher in self._log_watchers[app_name]:
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()

            if DEBUG_MODE:
                logger.debug(f"Watching: {watcher.path}")

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
                    logger.debug(f"TeamViewer log not found: {path}")
                continue
            watcher = LogWatcher(path, MonitorConfig.POLL_INTERVAL)
            self._log_watchers[app_name].append(watcher)

        # Unified backfill before starting threads
        self._backfill_app(app_name, self._log_watchers[app_name])

        for watcher in self._log_watchers[app_name]:
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()

            if DEBUG_MODE:
                logger.debug(f"Watching TeamViewer: {watcher.path}")

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

        # Unified backfill before starting threads
        self._backfill_app(app_name, self._log_watchers[app_name])

        for watcher in self._log_watchers[app_name]:
            t = threading.Thread(
                target=self._watch_log,
                args=(app_name, watcher),
                daemon=True
            )
            self._watcher_threads.append(t)
            t.start()

            if DEBUG_MODE:
                logger.debug(f"Watching VNC: {watcher.path}")

    def _start_crd_watchers(self):
        """Start watching Chrome Remote Desktop log files."""
        app_name = 'chrome_remote_desktop'
        self._session_trackers[app_name] = SessionTracker()
        self._log_watchers[app_name] = []

        # Chrome Remote Desktop log directory
        crd_log_dir = Path(MonitorConfig.APPDATA) / "Google" / "Chrome Remote Desktop" / "logs"

        if crd_log_dir.exists():
            for log_file in crd_log_dir.glob("*.log"):
                watcher = LogWatcher(log_file, MonitorConfig.POLL_INTERVAL)
                self._log_watchers[app_name].append(watcher)

            # Unified backfill before starting threads
            self._backfill_app(app_name, self._log_watchers[app_name])

            for watcher in self._log_watchers[app_name]:
                t = threading.Thread(
                    target=self._watch_log,
                    args=(app_name, watcher),
                    daemon=True
                )
                self._watcher_threads.append(t)
                t.start()

                if DEBUG_MODE:
                    logger.debug(f"Watching CRD: {watcher.path}")
        elif DEBUG_MODE:
            logger.debug(f"Chrome Remote Desktop logs not found: {crd_log_dir}")

    def _backfill_app(self, app_name: str, watchers: List[LogWatcher]) -> None:
        """
        One-shot bootstrap: read tail lines from ALL log files for an app,
        parse them, sort all events by timestamp, and replay through the
        single SessionTracker in chronological order.

        Why a unified pass instead of per-watcher backfill: AnyDesk writes to
        BOTH `ad.trace` (UI) and `ad_svc.trace` (service), and the order of
        events across files matters. A `session_stopped` from one file MUST
        be replayed before/after a `session_started` from the other based on
        wall-clock time — otherwise an old stop event from one stream can
        clobber the live session state from the other.

        Self-diagnostic: under DEBUG_MODE prints per-file readability/size
        and the resulting tracker direction.
        """
        tracker = self._session_trackers.get(app_name)
        if not tracker:
            return

        all_events: List[Tuple[datetime, dict, str]] = []
        per_file_diag: List[str] = []

        for watcher in watchers:
            path = watcher.path
            try:
                exists = path.exists()
                size = path.stat().st_size if exists else 0
            except (OSError, PermissionError) as e:
                per_file_diag.append(f"  ! {path.name}: stat-failed ({e})")
                continue

            if not exists:
                per_file_diag.append(f"  - {path.name}: missing")
                continue

            try:
                lines = watcher.read_tail_lines(max_lines=500)
            except Exception as e:
                per_file_diag.append(f"  ! {path.name}: read-failed ({e})")
                continue

            parsed = 0
            for line in lines:
                event = self._log_parser.parse_line(line)
                if not event:
                    continue
                ts = event.get("timestamp")
                # Default to "very old" so events without timestamp sort earliest
                ts = ts if isinstance(ts, datetime) else datetime.min
                all_events.append((ts, event, path.name))
                parsed += 1

            per_file_diag.append(
                f"  + {path.name}: size={size}B, lines_read={len(lines)}, events={parsed}"
            )

        # Sort chronologically across all files
        all_events.sort(key=lambda x: x[0])

        # Replay through the tracker
        for _, event, _src in all_events:
            tracker.on_event(event)

        # Clear stale sessions: if the backfill left an "active" session whose
        # start_time is older than 10 minutes, it belongs to a previous run —
        # not a currently live session. Keep it only if it started very recently
        # (agent restarted mid-session). Stale sessions cause a false session_started
        # to fire immediately, blocking detection of the real next session.
        cur = tracker.get_current_session()
        if cur and cur.active and cur.start_time:
            age_secs = (datetime.now() - cur.start_time).total_seconds()
            if age_secs > 600:
                tracker.on_event({"event": "session_stopped", "timestamp": datetime.now()})
                logger.info(
                    f"{app_name}: backfill cleared stale session "
                    f"(age={age_secs:.0f}s, dir={cur.direction}, id={cur.remote_id!r})"
                )

        if DEBUG_MODE:
            cur = tracker.get_current_session()
            dir_after = cur.direction if cur else "none"
            active_after = cur.active if cur else False
            start_after = cur.start_time.isoformat() if cur and cur.start_time else "-"
            logger.debug(f"{app_name}: backfill summary")
            for line in per_file_diag:
                logger.debug(line)
            logger.debug(
                f"  -> events_replayed={len(all_events)}, "
                f"current.direction={dir_after}, active={active_after}, start={start_after}"
            )

    def _watch_log(self, app_name: str, watcher: LogWatcher):
        """Thread: watch a single log file. Backfill is done up-front in
        `_backfill_app`; this thread only handles the live tail."""
        tracker = self._session_trackers.get(app_name)
        if not tracker:
            return

        logger.debug(f"tail started: {watcher.path} (pos={watcher._pos})")
        for line in watcher.tail(from_start=False):
            if not self._running:
                break

            if app_name == 'anydesk':
                logger.debug(f"new line from {watcher.path.name}: {line.rstrip()!r}")

            event = self._log_parser.parse_line(line)
            if event:
                logger.debug(f"[LOG] {app_name} | event={event.get('event')} | raw={line.rstrip()!r}")
                change_type = tracker.on_event(event)
                if change_type:
                    logger.info(f"[STATE] {app_name}: {change_type}")

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

    def _infer_direction_from_processes(
        self,
        processes: List[psutil.Process],
        listen_ports: List[int],
        check_outgoing: bool = True,
    ) -> str:
        """
        Infer session direction from connection topology, when log parsing
        does not provide it (e.g., RDP, RemotePC, Splashtop have no log).

        OUTGOING: a process owns an ESTABLISHED conn whose REMOTE port is in
                  `listen_ports` — we initiated a connection to a remote server.
        INCOMING: a process owns an ESTABLISHED conn whose LOCAL port is in
                  `listen_ports` — a remote client connected to our listener.

        check_outgoing=False skips the raddr check. Use for AnyDesk: its
        reverse hole-punch (local->remote:7070) creates raddr:7070 connections
        even during INCOMING sessions, which would falsely read as OUTGOING.
        """
        if not processes or not listen_ports:
            return SessionDirection.UNKNOWN
        port_set = set(listen_ports)
        for proc in processes:
            try:
                for c in proc.net_connections():
                    if c.status != 'ESTABLISHED':
                        continue
                    if check_outgoing and c.raddr and c.raddr.port in port_set:
                        return SessionDirection.OUTGOING
                    if c.laddr and c.laddr.port in port_set:
                        return SessionDirection.INCOMING
            except (psutil.AccessDenied, AttributeError, psutil.NoSuchProcess):
                continue
        return SessionDirection.UNKNOWN

    def _infer_anydesk_direction_from_history(self, max_age_minutes: int = 30) -> str:
        """
        Read connection_trace.txt for the most recent AnyDesk session within
        the last `max_age_minutes` and return its direction.

        Caveat: AnyDesk writes to connection_trace.txt at session END, not
        START. So during an in-progress session the file holds no entry yet.
        However, when log parsing missed the start (agent restarted mid-session)
        and the session subsequently ended, this gives us the direction
        retroactively for the next alert. Filtered by age to avoid using
        ancient entries that have nothing to do with the current connection.
        """
        if not hasattr(self, "_history_reader"):
            self._history_reader = HistoryReader()
        cutoff = datetime.now() - timedelta(minutes=max_age_minutes)
        try:
            entries = self._history_reader.read_anydesk_connection_trace(
                limit=10, since=cutoff
            )
        except Exception as e:
            logger.debug(f"AnyDesk history fallback failed: {e}")
            return SessionDirection.UNKNOWN
        if not entries:
            return SessionDirection.UNKNOWN
        # entries is chronological; last is most recent
        return entries[-1].get("direction") or SessionDirection.UNKNOWN

    def _get_system_listen_port_connections(self, listen_ports: List[int]) -> List[dict]:
        """
        System-wide search for ESTABLISHED connections whose LOCAL port is in
        `listen_ports`. Used for server-side detection of apps whose own
        process is svchost (e.g., RDP via TermService) so per-process
        net_connections() returns nothing.

        Note: psutil.net_connections() may require admin privileges on Windows
        for connections owned by other users; results are best-effort.
        """
        if not listen_ports:
            return []
        port_set = set(listen_ports)
        results: List[dict] = []
        try:
            for c in psutil.net_connections(kind='inet'):
                if c.status != 'ESTABLISHED':
                    continue
                if not c.laddr or not c.raddr:
                    continue
                if c.laddr.port not in port_set:
                    continue
                ip = c.raddr.ip
                # Filter localhost / link-local
                if ip.startswith('127.') or ip == '::1':
                    continue
                if self.is_whitelisted(ip, c.raddr.port):
                    continue
                results.append({
                    'remote_ip':   ip,
                    'remote_port': c.raddr.port,
                    'local_port':  c.laddr.port,
                    'status':      c.status,
                })
        except (psutil.AccessDenied, OSError, RuntimeError) as e:
            logger.debug(f"_get_system_listen_port_connections failed: {e}")
        return results

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
            # No matching named process — but some apps run inside a Windows
            # service hosted by svchost (e.g., RDP via TermService). Try
            # service + system-wide listen-port detection before declaring
            # the app "not running".
            service_names_cfg = app_config.get('service_names', [])
            listen_ports_cfg = app_config.get('listen_ports', [])

            svc_running = False
            for svc_name in service_names_cfg:
                if self.check_windows_service(svc_name):
                    svc_running = True
                    break

            sys_conns = self._get_system_listen_port_connections(listen_ports_cfg)

            if not svc_running and not sys_conns:
                # Truly not running
                return RemoteAppStatus(
                    app_name=app_name, app_id=app_config['id'], is_running=False,
                    has_active_session=False, process_count=0,
                    connection_count=0, connection_status=ConnectionStatus.CLOSED
                )

            # Service running OR an established session on listen port → treat as running
            has_session = bool(sys_conns)
            final_ip = sys_conns[0]['remote_ip'] if sys_conns else None
            conn_status = ConnectionStatus.OPEN if has_session else ConnectionStatus.CLOSED

            # GeoIP for the remote IP (best-effort)
            remote_country = None
            remote_country_code = None
            if final_ip:
                try:
                    geo = get_geolocator()
                    info = geo.get_country(final_ip)
                    remote_country = info.get('country')
                    remote_country_code = info.get('country_code')
                except Exception:
                    geo_result = GeoIPLookup.lookup(final_ip)
                    remote_country = geo_result.get('country')
                    remote_country_code = geo_result.get('country_code')

            return RemoteAppStatus(
                app_name=app_name,
                app_id=app_config['id'],
                is_running=True,
                has_active_session=has_session,
                process_count=0,
                connection_count=len(sys_conns),
                connection_status=conn_status,
                remote_ip=final_ip,
                # Server-side detection only sees incoming sessions
                direction=SessionDirection.INCOMING if has_session else SessionDirection.UNKNOWN,
                confidence='high' if has_session else 'medium',
                remote_country=remote_country,
                remote_country_code=remote_country_code,
                software=app_name,
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

        # Fallback: if no log direction (apps without log parsing — RDP,
        # RemotePC, Splashtop), infer from connection topology vs listen_ports.
        if log_direction == SessionDirection.UNKNOWN:
            listen_ports_cfg = app_config.get('listen_ports', [])
            if listen_ports_cfg:
                inferred = self._infer_direction_from_processes(
                    processes, listen_ports_cfg
                )
                # AnyDesk maintains raddr:7070 (relay hole-punch) even in standby —
                # topology always returns OUTGOING, making has_active_session=True
                # permanently while AnyDesk is open. This blocks session_started
                # from firing on the 2nd+ session. Now that log watchers run,
                # the log is the reliable source; topology OUTGOING is discarded.
                if app_name == 'anydesk' and inferred == SessionDirection.OUTGOING:
                    pass  # leave as UNKNOWN — log watcher will confirm direction
                else:
                    log_direction = inferred

        # AnyDesk-specific fallback: outgoing AnyDesk traffic goes to relay on
        # port 443, not 7070, so topology inference returns UNKNOWN. Read the
        # most recent entry from connection_trace.txt — written when a session
        # ends, but if the entry is from the last few minutes it's a strong
        # hint about an in-progress (or just-closed) session direction.
        anydesk_history_dir = SessionDirection.UNKNOWN
        if (log_direction == SessionDirection.UNKNOWN
                and app_name == 'anydesk'
                and suspicious_conn > 0):
            anydesk_history_dir = self._infer_anydesk_direction_from_history()
            log_direction = anydesk_history_dir

        # Diagnostic: AnyDesk active session but direction still UNKNOWN.
        # Captures full state so we can debug WHY the direction wasn't resolved.
        if (app_name == 'anydesk'
                and suspicious_conn > 0
                and log_direction == SessionDirection.UNKNOWN):
            try:
                tracker_state = "no-tracker"
                if session_tracker is not None:
                    cur = session_tracker.get_current_session()
                    if cur is None:
                        tracker_state = "no-current-session"
                    else:
                        tracker_state = (
                            f"dir={cur.direction}, active={cur.active}, "
                            f"start={cur.start_time}, remote_id={cur.remote_id!r}"
                        )

                file_states = []
                for w in self._log_watchers.get(app_name, []):
                    p = w.path
                    try:
                        if p.exists():
                            st = p.stat()
                            file_states.append(
                                f"{p.name}(size={st.st_size},"
                                f"mtime={datetime.fromtimestamp(st.st_mtime).isoformat()})"
                            )
                        else:
                            file_states.append(f"{p.name}(missing)")
                    except (OSError, PermissionError) as e:
                        file_states.append(f"{p.name}(stat-failed:{e})")

                listen_ports_cfg = app_config.get('listen_ports', [])
                topology_dir = (
                    self._infer_direction_from_processes(processes, listen_ports_cfg)
                    if listen_ports_cfg else SessionDirection.UNKNOWN
                )

                logger.warning(
                    "[DIAG] anydesk has active session but "
                    "direction=UNKNOWN. tracker={tracker} | logs=[{files}] | "
                    "topology_fallback={topo} | history_fallback={hist} | "
                    "suspicious_conn={sc} remote_ip={ip}".format(
                        tracker=tracker_state,
                        files=", ".join(file_states),
                        topo=topology_dir,
                        hist=anydesk_history_dir,
                        sc=suspicious_conn,
                        ip=remote_ip,
                    )
                )
            except Exception as diag_exc:
                logger.debug(f"AnyDesk UNKNOWN-direction diagnostic failed: {diag_exc}")

        # Check Windows service if configured
        service_running = False
        service_names = app_config.get('service_names', [])
        if service_names:
            for svc_name in service_names:
                if self.check_windows_service(svc_name):
                    service_running = True
                    break

        # Build signals for confidence calculation
        direction_known = (log_direction or '').lower() in ('incoming', 'outgoing')

        signals = {
            'active_connection': suspicious_conn > 0,
            'log_session_active': log_session_active,
            'cpu_active': cpu_usage > 5.0,
            'service_running': service_running,
            'direction_known': direction_known,
        }

        confidence = calculate_confidence(signals)

        has_active_session = any([
            signals['active_connection'],
            signals['log_session_active'],
            direction_known,
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
            # Session identity / forensics (populated when log/trace provides)
            remote_id=session.remote_id if session and session.remote_id else None,
            remote_name=session.remote_name if session and session.remote_name else None,
            logged_user=session.logged_user if session and session.logged_user else None,
            connection_id=session.connection_id if session and session.connection_id else None,
            software=(session.software if session and session.software else app_name),
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

    def get_next_poll_interval(
        self,
        last_results: Optional[Dict[str, RemoteAppStatus]] = None,
        idle_seconds: float = 30.0,
        active_seconds: float = 5.0,
        pending_seconds: float = 1.0,
    ) -> float:
        """
        Adaptive poll interval based on current state.

        Tiers (fastest wins):
          - pending: a debounced close/session-end is ticking — poll fast so
                     the alert fires within ~1s of the underlying change.
          - active : at least one remote-access app is running OR has an
                     active session.
          - idle   : nothing of interest — back off to save CPU.
        """
        if self._state_tracker.has_pending_events:
            return pending_seconds

        if last_results:
            for app_name, s in last_results.items():
                if s.is_running or s.has_active_session:
                    # AnyDesk relay incoming: session appears only when log parser
                    # fires (~2-3 s after connect). Poll faster so we don't miss it.
                    if app_name == 'anydesk' and s.is_running:
                        return min(active_seconds, 2.0)
                    return active_seconds

        return idle_seconds

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

    def get_session_history(self, hours: int = 24, limit_per_source: int = 50) -> List[dict]:
        """
        Read past session records from disk (AnyDesk + TeamViewer).
        Used at startup to surface sessions that completed while the agent
        was not running. Caller decides whether to alert / persist / ignore.
        """
        if not hasattr(self, "_history_reader"):
            self._history_reader = HistoryReader()
        return self._history_reader.read_all_recent(hours=hours, limit_per_source=limit_per_source)

    def get_anydesk_id(self) -> Optional[str]:
        """Return the local AnyDesk ID, or None if AnyDesk is not installed."""
        if not hasattr(self, "_anydesk_cli"):
            self._anydesk_cli = AnyDeskCLI()
        return self._anydesk_cli.get_id()

    def disconnect_remote_session(self, app_name: str) -> dict:
        """
        Force-disconnect an active remote session for the given app.
        Bridges backend ProtectiveAction `BlockRemoteAccess` to a local effect.

        Currently supported:
          - 'anydesk'     -> uses `AnyDesk.exe --disconnect`
          - 'teamviewer'  -> uses `TeamViewer.exe --action disconnect`;
                            falls back to process kill (teamviewer.exe) as a
                            last resort when the CLI flag is unavailable.

        Returns a result dict with at minimum:
          {"success": bool, "reason": str}
        and optionally "app": str when the tool is not supported.

        The caller must check "success" — a False result means the session
        may still be active and the user should be notified.
        """
        app = (app_name or "").lower()
        if app == "anydesk":
            if not hasattr(self, "_anydesk_cli"):
                self._anydesk_cli = AnyDeskCLI()
            ok = self._anydesk_cli.disconnect()
            if ok:
                return {"success": True, "reason": "disconnected"}
            if not self._anydesk_cli.is_available():
                return {"success": False, "reason": "cli_not_found"}
            return {"success": False, "reason": "disconnect_failed"}

        if app == "teamviewer":
            if not hasattr(self, "_teamviewer_cli"):
                self._teamviewer_cli = TeamViewerCLI()
            return self._teamviewer_cli.disconnect()

        logger.info(f"disconnect_remote_session: no CLI integration for '{app_name}'")
        return {"success": False, "reason": "unsupported", "app": app_name}

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
    import logging
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
    import time
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        monitor.stop_realtime_monitoring()
        print("\nStopped.")
