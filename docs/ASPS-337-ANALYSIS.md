# ASPS-337: Upgrade Agent Logic Monitoring Remote Access

## 📊 Analysis Summary
**Date:** 2026-03-23
**Status:** ASPS-339 (Learn new logic) - In Progress

---

## 🔍 What Already Exists in Agent

### 1. remote_monitor.py (45KB)
Already has:
- ✅ AnyDesk monitoring (partial)
- ✅ GeoIPLookup class with ip-api.com
- ✅ RemoteAppStatus dataclass
- ✅ StateChange dataclass
- ✅ MonitorConfig with paths
- ✅ LogParser class (basic)

### 2. detection/log_parsers.py (25KB)
Already has:
- ✅ LogPatterns class with comprehensive RegEx
- ✅ AnyDesk patterns (RE_INCOMING, RE_INCOMING_IP, etc.)
- ✅ TeamViewer patterns (RE_TV_INCOMING, RE_TV_CONN_INCOMING)
- ✅ VNC patterns (RE_VNC_ACCEPT, RE_VNC_X11_ACCEPT)
- ✅ parse_anydesk_trace() function
- ✅ parse_anydesk_svc_trace() function

### 3. models.py - RemoteAccessAlert
Current fields:
```python
@dataclass
class RemoteAccessAlert:
    token: str
    deviceInfo: DeviceInfo
    ConnectionUrl: str          # ✅ exists
    RemoteAccessApp: int        # ✅ exists
    RunningProcesses: int       # ✅ exists
    ConnectionStatus: int       # ✅ exists
    ConnectionsCount: int       # ✅ exists
    SessionStatus: int          # ✅ exists
```

---

## ❌ What's Missing

### 1. RemoteAccessAlert - New Fields Required
```python
# Need to add:
Direction: str              # 'incoming', 'outgoing', 'unknown'
RemoteIP: str               # IP of remote party
RemoteOS: str               # 'iOS', 'Windows', 'Android', etc.
RemoteVersion: str          # Remote app version
ConnectionType: str         # 'direct' or 'relay'
FileTransferActive: bool    # Is file transfer in progress?
FileTransferCount: int      # Number of file transfers
GeoCountry: str             # Country from GeoIP
GeoCity: str                # City from GeoIP
RemoteId: str               # Remote AnyDesk/TV ID
```

### 2. TeamViewer Support
- Patterns exist but no parser functions!
- Need: `parse_teamviewer_connections()` 
- Need: `parse_teamviewer_logfile()`
- Log paths:
  - `%APPDATA%\TeamViewer\Connections_incoming.txt`
  - `%APPDATA%\TeamViewer\Connections.txt`
  - `TeamViewer15_Logfile.log`

### 3. VNC Support
- Patterns exist but no parser functions!
- Need: `parse_vnc_logs()`
- Need: process detection for VNC servers
- Log locations vary by VNC variant

### 4. Chrome Remote Desktop Support
- NO patterns exist!
- Need: CRD log patterns
- Need: `parse_crd_logs()`
- Log path: `%APPDATA%\Google\Chrome Remote Desktop\logs\`

### 5. Backend Updates Required
- `RemoteAccessAlert.cs` - add new fields
- `RemoteAccessAlertView.cs` - add new fields
- Admin page - display new fields
- Database migration - new columns

---

## 📁 Key Files Comparison

| Feature | anydesk_monitor (9).py | Agent Current |
|---------|------------------------|---------------|
| AnyDesk parsing | ✅ Complete | ✅ Partial |
| TeamViewer parsing | ✅ Complete | ⚠️ Patterns only |
| VNC parsing | ✅ Complete | ⚠️ Patterns only |
| Chrome RD parsing | ✅ Complete | ❌ Missing |
| GeoIP lookup | ✅ ip-api.com | ✅ Same |
| File transfer detect | ✅ Complete | ⚠️ Patterns exist |
| Connection type | ✅ direct/relay | ⚠️ Patterns exist |
| Remote OS/Version | ✅ Complete | ⚠️ Patterns exist |
| Process monitoring | ✅ psutil | ✅ Same |

---

## 🎯 Implementation Plan

### Phase 1: Extend RemoteAccessAlert Model
1. Add new fields to `models.py`
2. Update `RemoteAccessAlert.to_json()` to include new fields
3. Update backend `RemoteAccessAlert.cs` to receive new fields

### Phase 2: Implement Missing Parsers
1. Create `parse_teamviewer_connections()` - use existing patterns
2. Create `parse_teamviewer_logfile()` - use existing patterns
3. Create `parse_vnc_logs()` - use existing patterns
4. Add Chrome Remote Desktop patterns and parser

### Phase 3: Integrate into Monitor
1. Update `remote_monitor.py` to call all parsers
2. Merge results from multiple log sources
3. Populate new RemoteAccessAlert fields
4. Send enriched alerts to backend

### Phase 4: Backend & Admin
1. Update database schema
2. Update Admin display

---

## ⚠️ Critical Security Implications

### File Transfer Detection
**HIGH PRIORITY** - File transfer during remote access session is a critical indicator of:
- Data exfiltration
- Malware installation
- Financial theft (downloading banking trojans)

### iOS Remote OS
When `remote_os == "iOS"`:
- Very suspicious! iOS users can't use AnyDesk to control a PC
- Strong indicator of scam "tech support" scenario
- Should trigger elevated risk score

### Direct Connection Type
When `connection_type == "direct"`:
- Higher risk than relay
- Faster data transfer possible
- Harder to trace

---

## 📋 Subtasks Mapping

| Subtask | Files to Modify |
|---------|-----------------|
| ASPS-339 Learn | This document ✅ |
| ASPS-340 TeamViewer | log_parsers.py, remote_monitor.py |
| ASPS-341 VNC | log_parsers.py, remote_monitor.py |
| ASPS-342 Chrome RD | log_parsers.py, remote_monitor.py |
| ASPS-343 GeoIP | Already exists, just wire up |
| ASPS-344 File Transfer | log_parsers.py (wire up patterns) |
| ASPS-345 Conn Type | log_parsers.py (wire up patterns) |
| ASPS-346 Remote OS | log_parsers.py (wire up patterns) |
| ASPS-347 AnyDesk patterns | log_parsers.py (verify/update) |
| ASPS-348 Tests | test.py, new test files |
| ASPS-349 Regression | E2E testing |

---

## 🔧 Technical Notes

### Log File Locations (Windows)
```
AnyDesk:
- %PROGRAMDATA%\AnyDesk\ad_svc.trace
- %APPDATA%\AnyDesk\ad.trace
- %APPDATA%\AnyDesk\connection_trace.txt

TeamViewer:
- %APPDATA%\TeamViewer\Connections_incoming.txt
- %APPDATA%\TeamViewer\Connections.txt
- %APPDATA%\TeamViewer\TeamViewer15_Logfile.log

Chrome Remote Desktop:
- %APPDATA%\Google\Chrome Remote Desktop\logs\

VNC (varies):
- TigerVNC: stdout or syslog
- UltraVNC: %ProgramFiles%\uvnc bvba\UltraVNC\ultravnc.log
```

### Ports Reference
```
AnyDesk: 7070, 7071, 443, 80
TeamViewer: 5938, 443, 80
VNC: 5900-5903
Chrome RD: 443 (WebRTC)
```

---

## ✅ ASPS-339 Completion Criteria
- [x] Read and understand anydesk_monitor (9).py
- [x] Document what exists in current agent
- [x] Identify gaps
- [x] Create implementation plan
- [x] Map subtasks to files

---

## 🔴 CRITICAL: Architecture Change Required

### Current Problem: Dual System
```
Agent has TWO parallel systems:
1. Real-time tailing (LogWatcher → SessionTracker)
2. Periodic polling (check_all() every 5 seconds)

They run simultaneously and try to merge results → MESSY!
```

### New Architecture: Event-Driven Only
```
LogWatcher.tail()
    → LogParser.parse_line()
        → SessionTracker.on_event()
            → Update State
                → Notify (StateChange)
```

**No polling. No merge. Clean.**

---

## 🔒 API CONTRACT - MUST NOT CHANGE

### RemoteAccessMonitor Public Methods
```python
# These signatures MUST remain unchanged:

def check_all(self) -> Dict[str, RemoteAppStatus]
def check_all_with_changes(self) -> Tuple[Dict[str, RemoteAppStatus], List[StateChange]]
def startup_scan(self) -> List[StateChange]
def get_active_sessions(self) -> List[RemoteAppStatus]
def has_any_active_session(self) -> bool
def start_realtime_monitoring(self)
def stop_realtime_monitoring(self)
```

### RemoteAppStatus Dataclass
```python
@dataclass
class RemoteAppStatus:
    app_name: str                    # KEEP
    app_id: int                      # KEEP
    is_running: bool                 # KEEP
    has_active_session: bool         # KEEP
    process_count: int               # KEEP
    connection_count: int            # KEEP
    connection_status: int           # KEEP
    remote_ip: Optional[str]         # KEEP
    direction: Optional[str]         # KEEP
    confidence: str                  # KEEP
    remote_country: Optional[str]    # KEEP
    remote_country_code: Optional[str]  # KEEP
    # Enhanced fields (already exist):
    remote_os: Optional[str]         # KEEP
    remote_version: Optional[str]    # KEEP
    connection_type: Optional[str]   # KEEP
    file_transfer_active: bool       # KEEP
    file_transfers: int              # KEEP
```

### StateChange Dataclass
```python
@dataclass
class StateChange:
    app_name: str           # KEEP
    change_type: str        # KEEP: 'opened', 'closed', 'session_started', 'session_ended'
    timestamp: datetime     # KEEP
    status: RemoteAppStatus # KEEP
    late_detection: bool    # KEEP
```

### Consumers (monitor_service.py)
```python
# These calls MUST continue to work:
state_changes = self.remote_monitor.startup_scan()
results, state_changes = self.remote_monitor.check_all_with_changes()
```

---

## 🔧 Implementation Strategy

### What Changes (Internal)
1. Remove `parse_tool_logs()` periodic calls
2. Add watchers for TeamViewer, VNC, Chrome RD
3. `check_all()` reads from SessionTrackers (not file parsing)
4. Single source of truth: SessionTracker state

### What Stays (External)
1. All public method signatures
2. All dataclass fields
3. All return types
4. Consumer code in monitor_service.py

### Migration Path
```
1. Add _start_teamviewer_watchers()
2. Add _start_vnc_watchers()  
3. Add _start_crd_watchers()
4. Modify check_all() to use trackers only
5. Remove parse_tool_logs() calls
6. Test: verify same behavior
```
