"""
Remote Monitor — Detection State Tracking and Confidence Scoring

Contains:
  - DetectionHistory     : rolling log of detection events for debugging
  - DebouncedStateTracker: debounced state-change emitter for remote-app transitions
  - calculate_confidence : signal-based confidence scoring

Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

import time
import logging
from collections import deque
from datetime import datetime
from typing import Dict, List, Optional

from remote_monitor_models import RemoteAppStatus, StateChange

logger = logging.getLogger(__name__)


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

    def __init__(self, close_debounce_seconds: float = 1.0, session_end_debounce_seconds: float = 4.0):
        self._close_debounce = close_debounce_seconds
        self._session_end_debounce = session_end_debounce_seconds
        self._pending_closes: Dict[str, float] = {}
        self._pending_session_ends: Dict[str, float] = {}
        self._previous_state: Dict[str, RemoteAppStatus] = {}

    @property
    def has_pending_events(self) -> bool:
        """True when there are debounced close/session-end events still ticking.
        Used by the monitor loop to switch to fast-poll mode so the alert
        fires within ~1s of the underlying state change."""
        return bool(self._pending_closes) or bool(self._pending_session_ends)

    def process_state(self, app_name: str, current_status: RemoteAppStatus) -> Optional[StateChange]:
        """Process current state and return a StateChange if a transition occurred."""
        prev = self._previous_state.get(app_name)
        now = datetime.now()

        # While DangerMode is active (we're inside an ImmediateDanger event),
        # bypass debouncing entirely — every transition is reported immediately.
        # Imported lazily to avoid pulling services.* into module init.
        try:
            from services.danger_mode import danger_mode
            danger_active = danger_mode.active
        except Exception:
            danger_active = False

        # App just closed
        if prev and prev.is_running and not current_status.is_running:
            self._pending_session_ends.pop(app_name, None)
            self._previous_state[app_name] = current_status
            if danger_active:
                # Bypass debounce — emit the close immediately
                return StateChange(
                    app_name=app_name,
                    change_type='closed',
                    timestamp=now,
                    status=prev,
                )
            # Normal: schedule a pending close (debounced)
            self._pending_closes[app_name] = time.time()
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
            # Session just ended
            if not current_status.has_active_session and prev.has_active_session:
                self._previous_state[app_name] = current_status
                if danger_active:
                    # Bypass debounce — emit the session_ended immediately
                    return StateChange(
                        app_name=app_name,
                        change_type='session_ended',
                        timestamp=now,
                        status=current_status,
                    )
                # Normal: schedule pending session end
                self._pending_session_ends[app_name] = time.time()
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
