# DeviceAlert Foreign Key Relationships

## ✅ IMPLEMENTED

`DeviceAlertEntity` now has proper EF Core foreign key relationships to `User` and `UserDevice`.

---

## 📊 Entity Structure

### **DeviceAlertEntity** (Updated)

```csharp
public abstract class DeviceAlertEntity : Entity, IDeviceAlert  // ← Implements IDeviceAlert
{
    // Foreign Keys (stored in database)
    public string? UserKeyField { get; set; }      // FK → User.KeyField
    public string? DeviceKeyField { get; set; }    // FK → UserDevice.KeyField (NEW!)
    
    // Navigation Properties (EF Core managed)
    [ForeignKey(nameof(UserKeyField))]
    public User? User { get; set; }                // Navigation to User
    
    [ForeignKey(nameof(DeviceKeyField))]
    public UserDevice? Device { get; set; }        // Navigation to UserDevice
    
    // Computed Properties (not stored)
    [NotMapped]
    public Key? UserKey { get; set; }              // Wrapper for UserKeyField
    
    [NotMapped]
    public Key? DeviceKey { get; set; }            // Wrapper for DeviceKeyField
    
    // IDeviceAlert Implementation (not stored)
    [NotMapped]
    public DeviceInfo DeviceInfo                   // ← NEW! IDeviceAlert requirement
    {
        get
        {
            if (Device != null)  // Uses Device navigation property
            {
                return new DeviceInfo(
                    DeviceKey,
                    DeviceUid,
                    Device.AggregateVersionField.ToString(),
                    "", "", 0,
                    (OperatingSystemType)Device.OperatingSystem
                );
            }
            else  // Fallback when Device not loaded
            {
                return new DeviceInfo(
                    DeviceKey, DeviceUid, "0", "", "", 0, OperatingSystem
                );
            }
        }
    }
    
    // Other properties...
    public string DeviceUid { get; set; }          // Still here for quick reference
    public Priority Priority { get; set; }         // IDeviceAlert requirement
    public DateTime Timestamp { get; set; }        // IDeviceAlert requirement
}
```

### **IDeviceAlert Interface**

```csharp
public interface IDeviceAlert
{
    Priority Priority { get; }
    DeviceInfo DeviceInfo { get; }
    DateTime Timestamp { get; }
}
```

---

## 🔑 Database Schema

### **DeviceAlerts Table** (Updated)

```sql
CREATE TABLE DeviceAlerts (
    `Key` VARCHAR(36) PRIMARY KEY,
    UserKey VARCHAR(36),                    -- FK to Users.Key
    DeviceKey VARCHAR(36),                  -- FK to UserDevices.Key (NEW!)
    DeviceUid VARCHAR(255),                 -- Quick reference field
    AlertType VARCHAR(100),
    Priority INT,
    Timestamp DATETIME,
    -- ... other columns
    
    FOREIGN KEY (UserKey) REFERENCES Users(`Key`) 
        ON DELETE SET NULL,
    FOREIGN KEY (DeviceKey) REFERENCES UserDevices(`Key`) 
        ON DELETE SET NULL,
    
    INDEX idx_devicealerts_userkey (UserKey),
    INDEX idx_devicealerts_devicekey (DeviceKey),
    INDEX idx_devicealerts_deviceuid (DeviceUid)
);
```

---

## 🚀 Usage Examples

### **Example 1: Query with Navigation Properties (EF Core Eager Loading)**

```csharp
// In repository or handler
var alerts = await _context.DeviceAlerts
    .Include(a => a.User)              // Load User navigation property
    .Include(a => a.Device)            // Load Device navigation property
    .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-24))
    .ToListAsync();

// Now you can access without additional queries
foreach (var alert in alerts)
{
    Console.WriteLine($"User: {alert.User?.FirstName}");
    Console.WriteLine($"Device: {alert.Device?.Model}");
}
```

### **Example 2: Query Specific Alert with Related Data**

```csharp
public async Task<DeviceAlertEntity?> GetAlertWithDetailsAsync(Key alertKey)
{
    return await _context.DeviceAlerts
        .Include(a => a.User)
        .Include(a => a.Device)
        .FirstOrDefaultAsync(a => a.KeyField == alertKey.Value);
}

// Usage
var alert = await GetAlertWithDetailsAsync(alertKey);
if (alert != null)
{
    var userName = $"{alert.User?.FirstName} {alert.User?.LastName}";
    var deviceInfo = $"{alert.Device?.Make} {alert.Device?.Model}";
}
```

### **Example 3: Create Alert with Relationships**

```csharp
// When creating an alert, set the foreign keys
var alert = new UrlAlertEntity
{
    KeyField = Guid.NewGuid().ToString(),
    AlertType = "PhishingUrl",
    Url = "http://malicious-site.com",
    UserKeyField = user.KeyField,         // Set FK
    DeviceKeyField = device.KeyField,     // Set FK
    DeviceUid = device.DeviceUid,         // Still set for quick reference
    // ... other properties
};

await _context.DeviceAlerts.AddAsync(alert);
await _context.SaveChangesAsync();

// EF Core will automatically set User and Device navigation properties
```

### **Example 4: Update Alert Relationships**

```csharp
var alert = await _context.DeviceAlerts.FindAsync(alertKey.Value);
if (alert != null)
{
    // Change the related device
    alert.DeviceKeyField = newDevice.KeyField;
    alert.DeviceUid = newDevice.DeviceUid;  // Keep in sync
    
    await _context.SaveChangesAsync();
    // EF Core will update the FK and navigation property
}
```

### **Example 5: Using IDeviceAlert Interface**

```csharp
// Method that accepts any IDeviceAlert implementation
public void ProcessAlert(IDeviceAlert alert)
{
    Console.WriteLine($"Priority: {alert.Priority}");
    Console.WriteLine($"Time: {alert.Timestamp}");
    
    var info = alert.DeviceInfo;
    Console.WriteLine($"Device: {info.DeviceUid}");
    Console.WriteLine($"OS: {info.OperatingSystem}");
    Console.WriteLine($"Version: {info.AggregateVersion}");
}

// Use with DeviceAlertEntity (must load Device for full info)
var dbAlert = await _context.DeviceAlerts
    .Include(a => a.Device)
    .FirstOrDefaultAsync(a => a.KeyField == key);

ProcessAlert(dbAlert);  // Works - DeviceAlertEntity implements IDeviceAlert
```

---

## 📋 Repository Pattern Examples

### **DeviceAlertRepository with Navigation Properties**

```csharp
public class DeviceAlertRepository : IDeviceAlertRepository
{
    private readonly AppDbContext _context;

    public async Task<IEnumerable<DeviceAlertEntity>> GetRecentAlertsAsync(
        TimeSpan timeSpan, 
        bool includeRelated = false)
    {
        var query = _context.DeviceAlerts.AsQueryable();
        
        // Optionally include navigation properties
        if (includeRelated)
        {
            query = query
                .Include(a => a.User)
                .Include(a => a.Device);
        }
        
        var cutoffTime = DateTime.UtcNow.Subtract(timeSpan);
        
        return await query
            .Where(a => a.Timestamp >= cutoffTime)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }
    
    public async Task<DeviceAlertEntity?> GetByKeyWithDetailsAsync(Key key)
    {
        return await _context.DeviceAlerts
            .Include(a => a.User)
            .Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.KeyField == key.Value);
    }
}
```

---

## 🎯 Query Handler Example

### **Get Alert Details Query**

```csharp
public class GetAlertDetailsQuery : Query
{
    public GetAlertDetailsQuery()
    {
        QueryType = nameof(GetAlertDetailsQuery);
    }
    
    public string QueryType { get; set; }
    public Key AlertKey { get; set; }
    public bool IncludeUser { get; set; } = true;
    public bool IncludeDevice { get; set; } = true;
}

public class GetAlertDetailsQueryResult : QueryResult
{
    public DeviceAlertEntity? Alert { get; set; }
}

public class AlertQueryHandlers
{
    private readonly AppDbContext _context;

    public AlertQueryHandlers(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GetAlertDetailsQueryResult> HandleAsync(GetAlertDetailsQuery query)
    {
        try
        {
            var queryable = _context.DeviceAlerts.AsQueryable();
            
            // Include navigation properties based on query
            if (query.IncludeUser)
            {
                queryable = queryable.Include(a => a.User);
            }
            
            if (query.IncludeDevice)
            {
                queryable = queryable.Include(a => a.Device);
            }
            
            var alert = await queryable
                .FirstOrDefaultAsync(a => a.KeyField == query.AlertKey.Value);
            
            if (alert == null)
            {
                return new GetAlertDetailsQueryResult
                {
                    Success = false,
                    Message = "Alert not found"
                };
            }

            return new GetAlertDetailsQueryResult
            {
                Success = true,
                Alert = alert
            };
        }
        catch (Exception ex)
        {
            return new GetAlertDetailsQueryResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
```

---

## 📝 Admin Dashboard Example

### **Razor Page with Related Data**

```csharp
// Page Model
public class AlertsModel : PageModel
{
    private readonly CQRSClient _cqrsClient;

    public List<DeviceAlertEntity> Alerts { get; set; } = new();

    public async Task OnGetAsync(int hours = 24)
    {
        var query = new GetRecentAlertsQuery 
        { 
            Hours = hours,
            IncludeUser = true,
            IncludeDevice = true
        };
        
        var result = await _cqrsClient.SendQueryAsync<GetRecentAlertsQueryResult>(query);
        
        if (result.Success)
        {
            Alerts = result.Alerts;
        }
    }
}
```

```cshtml
<!-- Razor View -->
<table class="table">
    <thead>
        <tr>
            <th>Time</th>
            <th>Alert Type</th>
            <th>User</th>
            <th>Device</th>
            <th>Priority</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var alert in Model.Alerts)
        {
            <tr>
                <td>@alert.Timestamp.ToString("yyyy-MM-dd HH:mm")</td>
                <td>@alert.AlertType</td>
                <td>
                    @if (alert.User != null)
                    {
                        <span>@alert.User.FirstName @alert.User.LastName</span>
                    }
                    else
                    {
                        <span class="text-muted">Unknown</span>
                    }
                </td>
                <td>
                    @if (alert.Device != null)
                    {
                        <span>@alert.Device.Make @alert.Device.Model</span>
                        <br />
                        <small class="text-muted">@alert.Device.OperatingSystem</small>
                    }
                    else
                    {
                        <span class="text-muted">@alert.DeviceUid</span>
                    }
                </td>
                <td>
                    <span class="badge bg-@GetPriorityColor(alert.Priority)">
                        @alert.Priority
                    </span>
                </td>
            </tr>
        }
    </tbody>
</table>
```

---

## 🔧 Database Migration

### **Run Migration Script**

```bash
mysql -u root -p ASPSBackend2DB < ADD-DEVICEKEY-COLUMN.sql
```

This will:
1. Add `DeviceKey` column to `DeviceAlerts` table
2. Create index on `DeviceKey`
3. Add foreign key constraint to `UserDevices`
4. Verify UserKey FK exists and add if missing

---

## ⚠️ Important Notes

### **1. Foreign Key vs DeviceUid**

```csharp
// DeviceKeyField = Actual FK to UserDevices.Key (GUID)
public string? DeviceKeyField { get; set; }  // "abc-123-def-456"

// DeviceUid = Quick reference field (not a FK)
public string DeviceUid { get; set; }        // "DEVICE-001"
```

**Why keep both?**
- `DeviceKeyField` - Proper FK for EF Core relationships
- `DeviceUid` - Denormalized for quick queries without joins
- Performance: Can filter by DeviceUid without joining UserDevices table

### **2. Nullable Foreign Keys**

```csharp
public string? UserKeyField { get; set; }    // Nullable
public string? DeviceKeyField { get; set; }  // Nullable
```

**Why nullable?**
- Alert might be for unregistered device
- User/Device might be deleted (FK set to null)
- Flexibility for various alert scenarios

### **3. EF Core Lazy Loading NOT Enabled**

```csharp
// ❌ This won't work - navigation props are null by default
var alert = await _context.DeviceAlerts.FindAsync(key);
var userName = alert.User?.FirstName;  // NULL!

// ✅ Must explicitly Include()
var alert = await _context.DeviceAlerts
    .Include(a => a.User)
    .FirstOrDefaultAsync(a => a.KeyField == key);
var userName = alert.User?.FirstName;  // Works!
```

### **4. DeviceInfo Property Behavior**

```csharp
// DeviceInfo returns different data depending on whether Device is loaded

// Without .Include(a => a.Device)
var alert = await _context.DeviceAlerts.FindAsync(key);
var deviceInfo = alert.DeviceInfo;
// Returns: Limited data from flattened properties (DeviceUid, OperatingSystem)
// AggregateVersion = "0" (unknown)

// With .Include(a => a.Device)
var alert = await _context.DeviceAlerts
    .Include(a => a.Device)
    .FirstOrDefaultAsync(a => a.KeyField == key);
var deviceInfo = alert.DeviceInfo;
// Returns: Full data from Device navigation property
// AggregateVersion from Device.AggregateVersionField
```

**Best Practice:**
```csharp
// Always Include Device if you need DeviceInfo
var alerts = await _context.DeviceAlerts
    .Include(a => a.Device)  // ← Important for full DeviceInfo
    .ToListAsync();

foreach (var alert in alerts)
{
    var info = alert.DeviceInfo;  // Has complete data
    Console.WriteLine($"Version: {info.AggregateVersion}");
}
```

### **4. CASCADE DELETE Behavior**

```csharp
// Configured as: ON DELETE SET NULL
entity.HasOne(e => e.User)
    .WithMany()
    .HasForeignKey(e => e.UserKeyField)
    .OnDelete(DeleteBehavior.SetNull);
```

**What happens when:**
- User deleted → `UserKeyField` set to NULL, alert remains
- Device deleted → `DeviceKeyField` set to NULL, alert remains
- Alert keeps DeviceUid even if Device FK is null

---

## ✅ Benefits

### **1. Data Integrity**
- Database enforces relationships
- Can't reference non-existent User/Device
- Automatic cleanup with SET NULL

### **2. Query Performance**
- Efficient joins via foreign keys
- Indexes on FK columns
- EF Core optimized queries

### **3. Type Safety**
```csharp
// Strong typing
User? user = alert.User;
UserDevice? device = alert.Device;

// IntelliSense support
var userName = alert.User?.FirstName;
var deviceModel = alert.Device?.Model;
```

### **4. EF Core Features**
- Eager Loading: `.Include()`
- Lazy Loading: (if enabled)
- Explicit Loading: `.Entry().Reference().Load()`
- Change Tracking: Automatic

---

## 🎯 Summary

**What Changed:**
- ✅ Added `DeviceKeyField` column (FK to UserDevices)
- ✅ Added `Device` navigation property with `[ForeignKey]`
- ✅ Added `DeviceKey` computed property (Key wrapper)
- ✅ Configured EF Core relationships in AppDbContext
- ✅ Created migration script
- ✅ Implemented `IDeviceAlert` interface
- ✅ Added `DeviceInfo` computed property

**IDeviceAlert Implementation:**
- ✅ `Priority` property (already existed)
- ✅ `Timestamp` property (already existed)
- ✅ `DeviceInfo` property (NEW - computed from Device navigation property)

**DeviceInfo Behavior:**
- If `Device` loaded: Full data from Device.AggregateVersionField
- If `Device` not loaded: Fallback to flattened properties
- Enum mapping: `OperatingSystem` → `OperatingSystemType` (cast by value)

**How to Use:**
1. Run migration: `ADD-DEVICEKEY-COLUMN.sql`
2. Use `.Include(a => a.User).Include(a => a.Device)` in queries
3. Access navigation properties directly: `alert.User`, `alert.Device`
4. EF Core handles loading and tracking automatically

**Key Points:**
- Foreign keys properly defined in database
- Navigation properties load via `.Include()`
- NULL FKs when User/Device deleted
- DeviceUid kept for denormalized queries
