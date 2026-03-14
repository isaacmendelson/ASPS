"""
AntiScam Desktop App - Message Models
Matches the backend protocol
"""

import uuid
import platform
from datetime import datetime
from dataclasses import dataclass, field, asdict
from typing import List, Optional
import json

from config import (
    VERSION, Priority, OperatingSystem, 
    ConnectionStatus, RiskType, ProtectiveAction
)


def get_os_type() -> int:
    """Get current operating system type"""
    system = platform.system()
    if system == "Windows":
        return OperatingSystem.WINDOWS
    elif system == "Linux":
        return OperatingSystem.LINUX
    elif system == "Darwin":
        return OperatingSystem.MAC
    return OperatingSystem.WINDOWS


def get_timestamp() -> str:
    """Get current timestamp in expected format"""
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


@dataclass
class DeviceInfo:
    id: str
    ver: str = VERSION
    ip: str = ""
    userAgent: str = ""
    timezone: int = 2
    OperatingSystem: int = field(default_factory=get_os_type)
    
    def to_dict(self):
        return asdict(self)


@dataclass
class DeviceAuthRequest:
    email: str
    deviceInfo: DeviceInfo
    jsonTypeName: str = "DeviceAuthRequest"
    priority: int = Priority.LOW
    timestamp: str = field(default_factory=get_timestamp)
    
    def to_json(self) -> str:
        data = {
            "jsonTypeName": self.jsonTypeName,
            "priority": self.priority,
            "timestamp": self.timestamp,
            "deviceInfo": self.deviceInfo.to_dict(),
            "email": self.email
        }
        return json.dumps(data)


@dataclass
class DeviceAuthResponse:
    token: str
    isAuthorized: bool
    jsonTypeName: str = "DeviceAuthResponse"
    
    @classmethod
    def from_json(cls, data: dict) -> 'DeviceAuthResponse':
        return cls(
            token=data.get('token', ''),
            isAuthorized=data.get('isAuthorized', False)
        )


@dataclass
class Tracker:
    Type: str
    Value: str
    
    def to_dict(self):
        return {"Type": self.Type, "Value": self.Value}


@dataclass
class SuspiciousUrlAlert:
    token: str
    url: str
    deviceInfo: DeviceInfo
    trackers: List[Tracker] = field(default_factory=list)
    iFrameDomains: List[str] = field(default_factory=list)
    jsonTypeName: str = "SuspiceousUrlAlert"  # Note: typo matches backend
    priority: int = Priority.MEDIUM
    timestamp: str = field(default_factory=get_timestamp)
    
    def to_json(self) -> str:
        data = {
            "jsonTypeName": self.jsonTypeName,
            "token": self.token,
            "priority": self.priority,
            "timestamp": self.timestamp,
            "deviceInfo": self.deviceInfo.to_dict(),
            "url": self.url,
            "trackers": [t.to_dict() for t in self.trackers],
            "iFrameDomains": self.iFrameDomains
        }
        return json.dumps(data)


@dataclass
class SuspiciousUrlAlertResponse:
    score: int
    riskType: List[int]
    protectiveAction: int
    jsonTypeName: str = "SuspiceousUrlAlertResponse"
    
    @classmethod
    def from_json(cls, data: dict) -> 'SuspiciousUrlAlertResponse':
        return cls(
            score=data.get('score', 0),
            riskType=data.get('riskType', []),
            protectiveAction=data.get('protectiveAction', ProtectiveAction.NONE)
        )


@dataclass
class RemoteAccessAlert:
    token: str
    deviceInfo: DeviceInfo
    ConnectionUrl: str
    RemoteAccessApp: int
    RunningProcesses: int
    ConnectionStatus: int
    ConnectionsCount: int
    SessionStatus: int
    jsonTypeName: str = "RemoteAccessAlert"
    priority: int = Priority.HIGH
    timestamp: str = field(default_factory=get_timestamp)
    
    def to_json(self) -> str:
        data = {
            "jsonTypeName": self.jsonTypeName,
            "token": self.token,
            "priority": self.priority,
            "timestamp": self.timestamp,
            "deviceInfo": self.deviceInfo.to_dict(),
            "ConnectionUrl": self.ConnectionUrl,
            "RemoteAccessApp": self.RemoteAccessApp,
            "RunningProcesses": self.RunningProcesses,
            "ConnectionStatus": self.ConnectionStatus,
            "ConnectionsCount": self.ConnectionsCount,
            "SessionStatus": self.SessionStatus
        }
        return json.dumps(data)


@dataclass 
class RemoteAccessAlertResponse:
    """Response from server for RemoteAccessAlert"""
    action: int = ProtectiveAction.NONE
    message: str = ""
    
    @classmethod
    def from_json(cls, data: dict) -> 'RemoteAccessAlertResponse':
        return cls(
            action=data.get('action', ProtectiveAction.NONE),
            message=data.get('message', '')
        )


@dataclass
class TrackUrlAlert:
    """Track URL alert - tracks URL navigation and time spent on pages"""
    token: str
    deviceInfo: DeviceInfo
    Url: str
    FromUrl: str = ""
    Duration: int = 0
    ScamInProgressKey: str = ""
    IPAddress: str = ""
    UserAgent: str = ""
    TabId: str = ""
    Timezone: str = ""
    jsonTypeName: str = "TrackUrlAlert"
    priority: int = Priority.MEDIUM
    timestamp: str = field(default_factory=get_timestamp)
    
    def to_json(self) -> str:
        data = {
            "jsonTypeName": self.jsonTypeName,
            "token": self.token,
            "priority": self.priority,
            "timestamp": self.timestamp,
            "deviceInfo": self.deviceInfo.to_dict(),
            "Url": self.Url,
            "FromUrl": self.FromUrl,
            "Duration": self.Duration,
            "ScamInProgressKey": self.ScamInProgressKey,
            "IPAddress": self.IPAddress,
            "UserAgent": self.UserAgent,
            "TabId": self.TabId,
            "Timezone": self.Timezone
        }
        return json.dumps(data)


# Extension <-> App Messages

@dataclass
class ExtensionUrlCheck:
    """Message from extension to app"""
    url: str
    trackers: List[dict] = field(default_factory=list)
    iframes: List[str] = field(default_factory=list)
    
    @classmethod
    def from_json(cls, data: dict) -> 'ExtensionUrlCheck':
        return cls(
            url=data.get('url', ''),
            trackers=data.get('trackers', []),
            iframes=data.get('iframes', [])
        )


@dataclass
class ExtensionUrlResponse:
    """Response from app to extension"""
    score: int
    riskType: List[int]
    protectiveAction: int
    cached: bool = False
    
    def to_json(self) -> str:
        return json.dumps({
            "score": self.score,
            "riskType": self.riskType,
            "protectiveAction": self.protectiveAction,
            "cached": self.cached
        })
