# ActiveDeviceAlert Implementation - Summary

## Overview
Implemented the corrected user-specific analysis architecture with proper alert lifecycle management.

---

## Changes Made

### 1. **New Class: ActiveDeviceAlert.cs**
**Location:** `Business/RealtimeAnalysis/UserDomain/ActiveDeviceAlert.cs`

**Purpose:** Tracks device alerts with their analysis state and metadata

**Properties:**
```csharp
public string DeviceUid { get; set; }              // Source device
public DeviceAlert Alert { get; set; }             // The alert data
public DateTime Timestamp { get; set; }            // When received
public AnalyzerResult? AnalysisResult { get; set; } // NULL initially, filled when analysis completes
```

**Constructors:**
- Default constructor: Sets Timestamp automatically
- Parameterized: `ActiveDeviceAlert(string deviceUid, DeviceAlert alert)`

---

### 2. **Modified: UDAnalysis.cs**

#### **REMOVED (Wrong Architecture):**
```csharp
❌ public List<DeviceAlert> ActiveAlerts  // Old simple list
```

#### **ADDED (Correct Architecture):**
```csharp
✅ private List<ActiveDeviceAlert> _activeDeviceAlerts
✅ private List<ActiveDeviceAlert> _expiredDeviceAlerts
✅ private readonly int _alertExpiryDays
✅ private readonly int _alertDeletionDays

// Public read-only access
✅ public IReadOnlyList<ActiveDeviceAlert> ActiveDeviceAlerts
✅ public IReadOnlyList<ActiveDeviceAlert> ExpiredDeviceAlerts
```

#### **Updated Constructor:**
```csharp
public UDAnalysis(
    UDUser udUser, 
    List<ISpecificAnalyzer> analyzers, 
    ILogger<UDAnalysis> logger,
    int alertExpiryDays = 30,      // From appsettings
    int alertDeletionDays = 90)    // From appsettings
```

#### **Updated AnalyzeAsync Method:**
**Signature Changed:**
```csharp
// OLD: public async Task AnalyzeAsync(DeviceAlert newAlert)
// NEW: public async Task AnalyzeAsync(DeviceAlert newAlert, string deviceUid)
```

**New Flow:**
1. Create `ActiveDeviceAlert` with `AnalysisResult = null`
2. Add to `_activeDeviceAlerts` list
3. Run all analyzers
4. **Update `activeAlert.AnalysisResult`** when analysis completes
5. Call `CleanupOldAlerts()`

#### **New Method: CleanupOldAlerts()**
```csharp
private void CleanupOldAlerts()
{
    // Move alerts older than ExpiryDays to expired list
    // Delete expired alerts older than DeletionDays
}
```

**Logic:**
- Active alerts older than `_alertExpiryDays` → Move to `_expiredDeviceAlerts`
- Expired alerts older than `_alertDeletionDays` → Delete permanently

---

### 3. **Modified: UDAnalysisManager.cs**

#### **REMOVED (Wrong Architecture):**
```csharp
❌ private readonly Dictionary<string, UDAnalysis> _activeAnalyses  // Multiple analyses per device
```

#### **ADDED (Correct Architecture):**
```csharp
✅ private readonly UDAnalysis _analysis  // Single analysis per user
✅ public UDAnalysis Analysis => _analysis  // Public access
```

#### **Updated Constructor:**
- Reads `DeviceAlertExpiryDays` from `appsettings.json` (default: 30)
- Reads `DeviceAlertDeletionDays` from `appsettings.json` (default: 90)
- Creates **ONE** `UDAnalysis` instance for the user with these settings
- Logs configuration values

#### **Updated HandleDeviceAlertAdded:**
**OLD:**
```csharp
// Get or create analysis per device
if (!_activeAnalyses.ContainsKey(deviceUid)) { ... }
var analysis = _activeAnalyses[deviceUid];
await analysis.AnalyzeAsync(alertEvent.Alert);
```

**NEW:**
```csharp
// Pass to single analysis with deviceUid
await _analysis.AnalyzeAsync(alertEvent.Alert, deviceUid);
```

---

### 4. **Modified: appsettings.json**

#### **Added Configuration Section:**
```json
"Analysis": {
  "DeviceAlertExpiryDays": 30,
  "DeviceAlertDeletionDays": 90
}
```

**Usage:**
- `DeviceAlertExpiryDays`: Days before active alert moves to expired
- `DeviceAlertDeletionDays`: Days before expired alert is deleted

---

## Architecture Flow

### **Alert Reception Flow:**
```
1. DeviceAlert arrives from Device A
   ↓
2. UserDomainManagerService routes to User's UDAnalysisManager
   ↓
3. UDAnalysisManager calls _analysis.AnalyzeAsync(alert, "DeviceA")
   ↓
4. UDAnalysis creates ActiveDeviceAlert:
   {
     DeviceUid: "DeviceA",
     Alert: [alert data],
     Timestamp: DateTime.UtcNow,
     AnalysisResult: null  ← Initially NULL
   }
   ↓
5. Add to _activeDeviceAlerts list
   ↓
6. Run analyzers
   ↓
7. Update activeAlert.AnalysisResult = [result]  ← Fill in result
   ↓
8. CleanupOldAlerts() runs
```

### **Single Analysis Per User:**
```
User A (has 3 devices: PC, Phone, Tablet)
  ↓
  UDAnalysisManager
    ↓
    ONE UDAnalysis instance
      ↓
      _activeDeviceAlerts = [
        { DeviceUid: "PC-001", Alert: {...}, Timestamp: ..., AnalysisResult: {...} },
        { DeviceUid: "Phone-002", Alert: {...}, Timestamp: ..., AnalysisResult: {...} },
        { DeviceUid: "PC-001", Alert: {...}, Timestamp: ..., AnalysisResult: {...} },
        { DeviceUid: "Tablet-003", Alert: {...}, Timestamp: ..., AnalysisResult: null }  ← Being analyzed
      ]
```

### **Alert Lifecycle:**
```
New Alert
  ↓ (received)
Active (_activeDeviceAlerts)
  ↓ (after DeviceAlertExpiryDays = 30 days)
Expired (_expiredDeviceAlerts)
  ↓ (after DeviceAlertDeletionDays = 90 days)
Deleted (removed from memory)
```

---

## Key Benefits

1. ✅ **Correct Architecture:** One analysis per user (not per device)
2. ✅ **Proper Tracking:** Each alert tracked with device source and analysis result
3. ✅ **Lifecycle Management:** Automatic expiry and deletion based on age
4. ✅ **State Management:** AnalysisResult starts null, filled when complete
5. ✅ **Configurable:** Expiry/deletion days set in appsettings.json
6. ✅ **Memory Efficient:** Old alerts automatically cleaned up

---

## Configuration Options

### **appsettings.json:**
```json
"Analysis": {
  "DeviceAlertExpiryDays": 30,     // Move to expired after 30 days
  "DeviceAlertDeletionDays": 90     // Delete expired after 90 days
}
```

**Adjust based on needs:**
- More history needed? → Increase both values
- Less memory usage? → Decrease both values
- Keep for analysis longer? → Increase ExpiryDays, keep DeletionDays
- Quick cleanup? → Decrease both values

---

## Testing

### **Build:**
```bash
CLEAN-BUILD.bat
dotnet build
```

### **Run:**
```bash
dotnet run --project ASPSBackend
```

### **Send Test Alert:**
Use `python-client-example_v2.py` to send UrlAlert

### **Expected Logs:**
```
[INFO] UDAnalysisManager created for user Key(User, 11111111...) with expiry=30d, deletion=90d
[INFO] Added alert from device PC-001. Total active alerts: 1
[INFO] Alert from device PC-001 analyzed. Severity: Medium, Active alerts: 1
```

### **Verify Lifecycle:**
- Wait for `DeviceAlertExpiryDays` → Alerts move to expired
- Wait for `DeviceAlertDeletionDays` → Expired alerts deleted
- Check logs for cleanup messages

---

## Files Modified/Created

### **Created:**
1. `Business/RealtimeAnalysis/UserDomain/ActiveDeviceAlert.cs` - New class

### **Modified:**
1. `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs` - New structure, lifecycle management
2. `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs` - Single analysis, config reading
3. `ASPSBackend/appsettings.json` - Added Analysis configuration section

**Total: 1 new file, 3 modified files**

---

## Summary

The implementation now correctly follows the specified architecture:
- ✅ One `UDAnalysis` per user (not per device)
- ✅ `ActiveDeviceAlert` tracks alert source, data, timestamp, and result
- ✅ `AnalysisResult` starts NULL, filled when analysis completes
- ✅ Automatic lifecycle management (active → expired → deleted)
- ✅ Configurable retention periods
- ✅ All alerts from user's devices in one unified list

Ready for testing! 🎉
