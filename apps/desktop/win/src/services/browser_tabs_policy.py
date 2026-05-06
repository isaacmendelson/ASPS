"""
BrowserTabs Policy
------------------

Decides whether to include BrowserTabs in a RemoteAccessAlert. The default
policy is "incoming_only" (include tabs only when remote-access direction is
'incoming' — someone is controlling THIS device).

Backend can temporarily override the default by publishing a
SetBrowserTabsPolicyNotification with:
    { Mode: "always" | "never" | "incoming_only", ValidUntil: <ISO datetime?> }

When ValidUntil passes the policy reverts to the configured default.
ValidUntil = None means the override stays in effect until a new override
arrives (or until the agent restarts — overrides are not persisted).
"""

import logging
import threading
from datetime import datetime, timezone
from typing import Optional

from config import BROWSER_TABS_DEFAULT_MODE

logger = logging.getLogger(__name__)


VALID_MODES = ("incoming_only", "always", "never")


class BrowserTabsPolicy:
    """Process-wide policy state for BrowserTabs inclusion. Thread-safe."""

    def __init__(self, default_mode: str = BROWSER_TABS_DEFAULT_MODE):
        if default_mode not in VALID_MODES:
            logger.warning(
                "Invalid BROWSER_TABS_DEFAULT_MODE %r; falling back to 'incoming_only'",
                default_mode,
            )
            default_mode = "incoming_only"
        self._default_mode = default_mode
        self._override_mode: Optional[str] = None
        self._override_valid_until: Optional[datetime] = None
        self._lock = threading.Lock()

    @property
    def default_mode(self) -> str:
        return self._default_mode

    def get_effective_mode(self) -> str:
        """Return the mode in effect right now.

        If an override is set and not yet expired, return it. Otherwise (no
        override, or override expired) return the configured default. Expired
        overrides are cleared on read so subsequent calls don't keep checking.
        """
        with self._lock:
            if self._override_mode is None:
                return self._default_mode

            if self._override_valid_until is None:
                # Permanent until a new override arrives
                return self._override_mode

            if datetime.now(timezone.utc) < self._override_valid_until:
                return self._override_mode

            # Expired — clear and revert
            logger.info(
                "BrowserTabs policy override expired (was %r, valid_until=%s) — reverting to default %r",
                self._override_mode,
                self._override_valid_until.isoformat(),
                self._default_mode,
            )
            self._override_mode = None
            self._override_valid_until = None
            return self._default_mode

    def set_override(self, mode: str, valid_until: Optional[datetime]) -> bool:
        """Apply an override. Returns True if accepted, False if mode invalid."""
        if mode not in VALID_MODES:
            logger.warning(
                "Rejecting BrowserTabs policy override: invalid mode %r (allowed: %s)",
                mode, VALID_MODES,
            )
            return False

        with self._lock:
            self._override_mode = mode
            self._override_valid_until = valid_until

        logger.info(
            "BrowserTabs policy override applied: mode=%r valid_until=%s",
            mode,
            valid_until.isoformat() if valid_until else "permanent",
        )
        return True

    def clear_override(self) -> None:
        with self._lock:
            self._override_mode = None
            self._override_valid_until = None
        logger.info("BrowserTabs policy override cleared — reverted to default %r", self._default_mode)


# Process-wide singleton — imported by both monitor_service and notification_handler
policy = BrowserTabsPolicy()


def parse_valid_until(value) -> Optional[datetime]:
    """Parse a ValidUntil value from a notification payload.

    Accepts ISO-8601 strings (with or without timezone) and returns a UTC-aware
    datetime. Naive datetimes are assumed to be UTC. Returns None if the input
    is None/empty/unparseable — meaning 'no expiry'.
    """
    if value is None or value == "":
        return None
    if isinstance(value, datetime):
        return value if value.tzinfo else value.replace(tzinfo=timezone.utc)
    if isinstance(value, str):
        try:
            # Handle the trailing 'Z' that .NET DateTime serializers often emit
            normalized = value[:-1] + "+00:00" if value.endswith("Z") else value
            dt = datetime.fromisoformat(normalized)
            return dt if dt.tzinfo else dt.replace(tzinfo=timezone.utc)
        except ValueError:
            logger.warning("Could not parse ValidUntil %r — treating as no expiry", value)
            return None
    logger.warning("Unexpected ValidUntil type %s — treating as no expiry", type(value).__name__)
    return None
