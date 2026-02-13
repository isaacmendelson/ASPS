# User-Specific Analysis Instances - Implementation Summary

## Changes Implemented

### 1. **New Class: UDUser.cs**
**Location:** `Business/RealtimeAnalysis/UserDomain/UDUser.cs`

**Purpose:** Runtime representation of a User with analysis state

**Properties:**
- `Key Key` - User's GUID key
- All properties from `User` entity EXCEPT `IsDeleted` and `KeyField`
- `IEnumerable<DeviceAlert> ActiveAlerts` - Current alerts for this user

**Methods:**
- `AddAlert(DeviceAlert alert)` - Add alert to user's active list
- `ClearAlerts()` - Clear all alerts
- `FullName` property - Returns formatted full name

---

### 2. **Modified: UDAnalysis.cs**
**Changes:**
- Added property: `public UDUser UDUser { get; private set; }`
- Updated constructor to accept `UDUser` as first parameter
- Constructor now: `UDAnalysis(UDUser udUser, List<ISpecificAnalyzer> analyzers, ILogger<UDAnalysis> logger)`

**Impact:** Each UDAnalysis instance is now tied to a specific user

---

### 3. **Modified: UDAnalysisManager.cs**
**Changes:**
- Changed `private readonly User _user` → `private readonly UDUser _udUser`
- Added property: `public UDUser UDUser => _udUser`
- Updated constructor to accept `UDUser` instead of `User`
- Updated all references from `_user` to `_udUser`
- When creating UDAnalysis instances, now passes `_udUser`

**Impact:** Manager is now user-specific and maintains UDUser instance

---

### 4. **New Class: UserDomainManagerService.cs**
**Location:** `Business/RealtimeAnalysis/UserDomain/UserDomainManagerService.cs`

**Purpose:** Central service managing all user-specific UDAnalysisManager instances

**Key Features:**
- Maintains `ConcurrentDictionary<string, UDAnalysisManager>` - one manager per user
- Thread-safe access to managers
- On-demand creation of managers

**Methods:**
- `GetOrCreateManagerForUser(Key userKey)` - Get/create manager for user
- `GetManagerForDeviceAsync(string deviceUid)` - Get manager by device UID
- `RemoveManagerForUser(Key userKey)` - Stop and remove manager
- `GetActiveManagerCount()` - Get count of active managers
- `StopAll()` - Stop all managers (for shutdown)
- `CreateUDUserFromEntity(UserEntity user)` - Convert entity to UDUser

**Lifecycle:**
- Registered as **Singleton** in DI container
- Lives for entire application lifetime
- Manages background instances per user

---

### 5. **Modified: RealTimeAlertListener.cs**
**Changes:**
- Added using: `using Business.RealtimeAnalysis.UserDomain;`
- Added field: `private readonly UserDomainManagerService _userDomainService`
- Updated constructor to inject `UserDomainManagerService`
- Modified alert routing logic:
  ```csharp
  // Old: Notify all event handlers
  foreach (var handler in _eventHandlers) { ... }
  
  // New: Route to user-specific manager
  var userManager = await _userDomainService.GetManagerForDeviceAsync(deviceUid);
  if (userManager != null) {
      userManager.Handle(domainEvent);
  }
  ```

**Impact:** Alerts are now routed directly to the specific user's manager

---

### 6. **Modified: Program.cs**
**Changes:**
- Added registration: `services.AddSingleton<UserDomainManagerService>()`
- Updated `RealTimeAlertListener` factory to inject `UserDomainManagerService`
- Rewrote `InitializeAnalysisManagersAsync()` to use `UserDomainManagerService`
  - Loads active users
  - Creates manager for each using `GetOrCreateManagerForUser()`
  - Displays count of initialized managers

**Impact:** Service is properly registered and initialized at startup

---

## Architecture

### Before:
```
Alert → RealTimeAlertListener → Event Handlers (all managers notified)
```

### After:
```
Alert → RealTimeAlertListener 
      → UserDomainManagerService.GetManagerForDeviceAsync(deviceUid)
      → Specific UDAnalysisManager for that user
      → User's UDAnalysis instances
```

### Per-User Instances:
```
User A → UDUser A → UDAnalysisManager A → UDAnalysis instances A
User B → UDUser B → UDAnalysisManager B → UDAnalysis instances B
User C → UDUser C → UDAnalysisManager C → UDAnalysis instances C
```

---

## Flow Diagram

```
1. Device sends alert
2. RealTimeAlertListener receives alert
3. Lookup device → get UserKey
4. UserDomainManagerService.GetManagerForDeviceAsync(deviceUid)
   a. Finds device in database
   b. Gets user key from device
   c. Returns existing manager OR creates new one
5. Manager.Handle(alertEvent)
6. Manager routes to appropriate UDAnalysis instance
7. Analysis runs with user-specific context
```

---

## Key Benefits

1. ✅ **Isolation** - Each user's analysis state is separate
2. ✅ **Scalability** - Managers created on-demand
3. ✅ **Efficiency** - Only relevant manager processes each alert
4. ✅ **State Management** - User context (UDUser) maintained throughout analysis
5. ✅ **Thread Safety** - ConcurrentDictionary ensures safe concurrent access

---

## Testing the Implementation

### 1. Build
```bash
CLEAN-BUILD.bat
dotnet build
```

### 2. Run
```bash
dotnet run --project ASPSBackend
```

### 3. Send Alert
Use python-client-example_v2.py or send UrlAlert via TCP

### 4. Observe Logs
Should see:
```
→ UDAnalysisManager initialized for user: John Doe
→ Total managers initialized: 2
[INFO] Alert routed to UDAnalysisManager for user: Key(User, 11111111-1111-1111-1111-111111111111)
```

---

## Future Enhancements (Not Implemented)

As you mentioned, you'll add more properties to UDUser later. The architecture now supports:
- Adding user preferences
- User-specific analysis configurations
- Historical analysis data per user
- User behavior patterns
- Risk scores
- etc.

---

## Files Modified/Created

### Created:
1. `Business/RealtimeAnalysis/UserDomain/UDUser.cs`
2. `Business/RealtimeAnalysis/UserDomain/UserDomainManagerService.cs`

### Modified:
1. `Business/RealtimeAnalysis/UserDomain/UDAnalysis.cs`
2. `Business/RealtimeAnalysis/UserDomain/UDAnalysisManager.cs`
3. `Business/Messaging/RealTimeAlertListener.cs`
4. `ASPSBackend/Program.cs`

**Total: 2 new files, 4 modified files**

---

## Backward Compatibility

- ✅ Existing event handler system still works (legacy support)
- ✅ ASView continues to function normally
- ✅ Database schema unchanged
- ✅ API endpoints unchanged

The new user-specific architecture sits alongside existing infrastructure without breaking changes.
