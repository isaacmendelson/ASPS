"""
AntiScam Desktop App - Enums
Matches Common.Enums from backend
"""

from enum import IntEnum


class AccountType(IntEnum):
    Email = 1
    Communication = 2
    Social = 3
    Financial = 4
    Other = 5


class DeviceType(IntEnum):
    Unknown = 0
    PersonalComputer = 1
    SmartPhone = 2
    Other = 3


class DeviceMonitoringStatus(IntEnum):
    Disabled = 0
    Enabled = 1


class OperatingSystemType(IntEnum):
    Unknown = 0
    Windows = 1
    Linux = 2
    Mac = 3
    Android = 4
    IOS = 5


class RemoteAccessApp(IntEnum):
    Unknown = 0
    AnyDesk = 1
    TeamViewer = 2
    ChromeRemoteDesktop = 3
    RemotePC = 4
    LogMeIn = 5
    Splashtop = 6
    VNC = 7


class UserRole(IntEnum):
    Unknown = 0
    Self = 1
    Guardian = 2
    Other = 3


class CautionLevel(IntEnum):
    Low = 0
    Medium = 1
    High = 2


class AlertFlagType(IntEnum):
    NONE = 0
    RemoteAccess_AppRunning = 1
    RemoteAccess_ConnectionOpen = 2
    RemoteAccess_SessionActive = 3


class AlertFlagStatus(IntEnum):
    Unknown = 0
    Open = 1
    Closed = 2


class ConnectionStatus(IntEnum):
    Unknown = 0
    Open = 1
    Closed = 2


class PersonalComputerType(IntEnum):
    Unknown = 0
    Desktop = 1
    Laptop = 2
    Tablet = 3


class Priority(IntEnum):
    Low = 0
    Medium = 1
    High = 2
    Critical = 3


class Severity(IntEnum):
    Low = 0
    Medium = 1
    High = 2
    Critical = 3


class ResultStatusCode(IntEnum):
    Success = 200
    InvalidOperation = 400
    ValidationError = 422
    ServerError = 500
    Unauthenticated = 401
    Unauthorized = 403
    NotFound = 404


class ProtectiveActionType(IntEnum):
    """
    Protective actions that can be taken
    """
    NONE = 0
    DisplayNotification = 1      # Show notification on device
    EmailNotification = 2         # Send email alert
    SoundAlert = 3                # Play alert sound
    BlockUrl = 4                  # Block the URL
    UserDisplayNotification = 5   # Show notification to user
    QuarantineDevice = 6          # Quarantine the device
    BlockRemoteAccess = 7         # Block remote access


class ProtectiveActionSubject(IntEnum):
    """
    Who should receive the protective action
    """
    NONE = 0
    Device = 1      # Action targets the device
    User = 2        # Action targets the user
    Protector = 3   # Action targets the protector/guardian


class AnalysisLevel(IntEnum):
    Unknown = 0
    Device = 1
    User = 2


# Helper functions
def get_protective_action_name(action_type: int) -> str:
    """Get human-readable name for protective action"""
    try:
        return ProtectiveActionType(action_type).name
    except ValueError:
        return f"Unknown({action_type})"


def get_severity_name(severity: int) -> str:
    """Get human-readable name for severity"""
    try:
        return Severity(severity).name
    except ValueError:
        return f"Unknown({severity})"


def get_subject_name(subject: int) -> str:
    """Get human-readable name for subject"""
    try:
        return ProtectiveActionSubject(subject).name
    except ValueError:
        return f"Unknown({subject})"
