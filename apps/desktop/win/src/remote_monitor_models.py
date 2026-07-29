"""
Remote Monitor — Data Models

Shared data structures for the remote access monitor.
Extracted from remote_monitor.py as part of the ASPS-627 split.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional


class SessionDirection:
    INCOMING = "incoming"
    OUTGOING = "outgoing"
    UNKNOWN = "unknown"


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
    # Session identity / forensics fields (populated when log/trace provides them)
    remote_id: Optional[str] = None       # AnyDesk numeric ID / TV Partner ID
    remote_name: Optional[str] = None     # Display name / hostname (TV)
    logged_user: Optional[str] = None     # Local user logged on at session time (TV forensics)
    connection_id: Optional[str] = None   # GUID of the session record (TV forensics)
    software: Optional[str] = None        # 'AnyDesk' / 'TeamViewer' / 'VNC' / 'ChromeRD'


@dataclass
class StateChange:
    """Represents a state change event for a remote access app."""
    app_name: str
    change_type: str  # 'opened', 'closed', 'session_started', 'session_ended'
    timestamp: datetime
    status: RemoteAppStatus
    late_detection: bool = False
