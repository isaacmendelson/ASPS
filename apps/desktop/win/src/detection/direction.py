"""
Direction detection for remote access sessions.

Determines whether a remote access session is:
- INCOMING (dangerous): Someone is connecting TO this computer
- OUTGOING (safe): User is connecting FROM this computer to elsewhere
- UNKNOWN: Cannot determine direction

Direction matters because incoming connections are the scam risk vector.
"""

from enum import Enum
from typing import Dict, Any, List, Optional
import logging

logger = logging.getLogger(__name__)


class Direction(Enum):
    """Session direction enum."""
    INCOMING = 'incoming'  # Someone connecting TO this computer (dangerous)
    OUTGOING = 'outgoing'  # User connecting FROM this computer (safe)
    UNKNOWN = 'unknown'    # Cannot determine


def detect_direction_from_port(
    connections: List[Dict[str, Any]],
    tool_config: Optional[Dict[str, Any]]
) -> Optional[Direction]:
    """
    Detect direction based on port analysis.

    For tools with listen_ports configured:
    - If local port matches listen_port: INCOMING (we're the server)
    - If remote port matches listen_port: OUTGOING (we're the client)

    Args:
        connections: List of connection dicts with 'local_port' and 'remote_port'
        tool_config: Tool configuration from get_tool_config()

    Returns:
        Direction if determined, None if cannot determine
    """
    if not tool_config or not connections:
        return None

    listen_ports = tool_config.get('listen_ports', [])
    if not listen_ports:
        return None

    for conn in connections:
        local_port = conn.get('local_port')
        remote_port = conn.get('remote_port')

        # If our local port is the listen port, we're the server (INCOMING)
        if local_port in listen_ports:
            logger.debug(f"Direction: INCOMING (local port {local_port} is listen port)")
            return Direction.INCOMING

        # If remote port is the listen port, we're the client (OUTGOING)
        if remote_port in listen_ports:
            logger.debug(f"Direction: OUTGOING (remote port {remote_port} is listen port)")
            return Direction.OUTGOING

    return None


def detect_direction_from_logs(log_signals: Dict[str, Any]) -> Optional[Direction]:
    """
    Extract direction from log parsing results.

    Log parsers already detect direction from tool-specific log patterns
    like "accept request from" (incoming) or "connecting to" (outgoing).

    Args:
        log_signals: Result from parse_tool_logs()

    Returns:
        Direction if logs indicate direction, None otherwise
    """
    log_direction = log_signals.get('log_direction')
    if not log_direction:
        return None

    if log_direction == 'incoming':
        logger.debug("Direction: INCOMING (from log analysis)")
        return Direction.INCOMING
    if log_direction == 'outgoing':
        logger.debug("Direction: OUTGOING (from log analysis)")
        return Direction.OUTGOING

    return None


def detect_direction(
    connections: List[Dict[str, Any]],
    tool_config: Optional[Dict[str, Any]],
    log_signals: Dict[str, Any]
) -> Direction:
    """
    Combined direction detection using multiple signals.

    Priority:
    1. Log-based detection (most reliable - app's own record)
    2. Port-based detection (network evidence)
    3. UNKNOWN if no signals

    Args:
        connections: List of connection dicts with port info
        tool_config: Tool configuration
        log_signals: Result from parse_tool_logs()

    Returns:
        Direction enum value (never None - returns UNKNOWN if undetermined)
    """
    # Priority 1: Log-based (most reliable)
    log_direction = detect_direction_from_logs(log_signals)
    if log_direction is not None:
        return log_direction

    # Priority 2: Port-based
    port_direction = detect_direction_from_port(connections, tool_config)
    if port_direction is not None:
        return port_direction

    # Cannot determine
    logger.debug("Direction: UNKNOWN (no signals)")
    return Direction.UNKNOWN
