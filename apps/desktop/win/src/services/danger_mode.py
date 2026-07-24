"""
DangerMode
----------

Process-wide flag indicating that the agent is currently inside an active
ImmediateDanger event (server told us via ImmediateDangerNotification, not yet
followed by ImmediateDangerEndedNotification).

While active:
  - monitor_service._monitor_remote_access polls every POLL_INTERVAL_SECONDS
    (overrides the adaptive interval).
  - DebouncedStateTracker bypasses close / session_end debouncing so any
    RemoteAccess state change is reported immediately.

Singleton — imported by both monitor_service and notification_handler.
Thread-safe.
"""

import logging
import threading
from datetime import datetime, timezone
from typing import Optional

from config import IMMEDIATE_DANGER_POLL_INTERVAL_SECONDS

logger = logging.getLogger(__name__)


class DangerMode:
    POLL_INTERVAL_SECONDS = IMMEDIATE_DANGER_POLL_INTERVAL_SECONDS

    def __init__(self):
        self._active = False
        self._activated_at: Optional[datetime] = None
        self._lock = threading.Lock()

    @property
    def active(self) -> bool:
        with self._lock:
            return self._active

    @property
    def activated_at(self) -> Optional[datetime]:
        with self._lock:
            return self._activated_at

    def activate(self) -> None:
        with self._lock:
            if self._active:
                return  # already active — idempotent
            self._active = True
            self._activated_at = datetime.now(timezone.utc)
        logger.info("[DangerMode] ACTIVATED — fast polling (every %.1fs) + no debounce",
                    self.POLL_INTERVAL_SECONDS)

    def deactivate(self) -> None:
        with self._lock:
            if not self._active:
                return
            self._active = False
            self._activated_at = None
        logger.info("[DangerMode] DEACTIVATED — reverting to adaptive polling + normal debounce")


# Process-wide singleton
danger_mode = DangerMode()
