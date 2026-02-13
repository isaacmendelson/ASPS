# Polymorphic Type Serialization in CQRS

## ❌ ERROR: "Could not create an instance of type DeviceAlertEntity"

This error occurs when trying to deserialize abstract or polymorphic types without type information.

---

## 🎯 THE PROBLEM

### **Polymorphic Types in Our System:**

1. **DeviceAlertEntity** (abstract)
   - UrlAlertEntity
   - RemoteAccessAlertEntity

2. **UserDevice** (abstract)
   - PersonalComputer
   - SmartPhone

3. **AnalysisResultContainer** (base)
   - UrlAnalysisResultContainer

When these are serialized to JSON without type information, the deserializer doesn't know which concrete type to create.

---

## ✅ THE SOLUTION

### **TypeNameHandling.Auto**

Newtonsoft.Json can include type information in the JSON:

```csharp
var settings = new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.Auto,
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
};

var json = JsonConvert.SerializeObject(obj, settings);
var obj = JsonConvert.DeserializeObject<T>(json, settings);
```

---

## 📊 HOW IT WORKS

### **Without TypeNameHandling:**

```json
{
  "Alerts": [
    {
      "AlertType": "UrlAlert",
      "Url": "http://phishing.com",
      "DeviceUid": "PC-001"
    }
  ]
}
```

**Problem:** Deserializer doesn't know if this is `UrlAlertEntity` or `RemoteAccessAlertEntity`.

### **With TypeNameHandling.Auto:**

```json
{
  "Alerts": [
    {
      "$type": "Common.Entities.UrlAlertEntity, Common",
      "AlertType": "UrlAlert",
      "Url": "http://phishing.com",
      "DeviceUid": "PC-001"
    }
  ]
}
```

**Solution:** `$type` field tells deserializer to create `UrlAlertEntity`! ✅

---

## 🔧 WHERE IT'S APPLIED

### **1. CQRSGateway (Business Layer)**

**When sending responses:**

```csharp
private async Task<string> HandleGetRecentAlertsQuery(...)
{
    var result = await handler.HandleAsync(query);
    
    // Serialize WITH type information
    return JsonConvert.SerializeObject(result, new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,           // ← KEY!
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    });
}
```

### **2. CQRSClient (WebApi)**

**When receiving responses:**

```csharp
// Deserialize WITH type information
var result = JsonConvert.DeserializeObject<TResult>(responseJson, new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.Auto  // ← KEY!
});
```

---

## 📋 ALL POLYMORPHIC TYPES

### **DeviceAlertEntity (Abstract)**

```csharp
public abstract class DeviceAlertEntity : Entity, IDeviceAlert
{
    // Base properties
}

public class UrlAlertEntity : DeviceAlertEntity
{
    public string Url { get; set; }
}

public class RemoteAccessAlertEntity : DeviceAlertEntity
{
    public string ConnectionUrl { get; set; }
}
```

**JSON with TypeNameHandling:**
```json
{
  "$type": "Common.Entities.UrlAlertEntity, Common",
  "Url": "http://example.com"
}
```

### **UserDevice (Abstract)**

```csharp
public abstract class UserDevice : Entity
{
    // Base properties
}

public class PersonalComputer : UserDevice
{
    public string MotherboardSerial { get; set; }
}

public class SmartPhone : UserDevice
{
    public string PhoneNumber { get; set; }
}
```

**JSON with TypeNameHandling:**
```json
{
  "$type": "Common.Entities.PersonalComputer, Common",
  "MotherboardSerial": "ABC123"
}
```

---

## ⚠️ IMPORTANT NOTES

### **1. TypeNameHandling.Auto is Safe**

- **Auto**: Only adds `$type` when needed (polymorphic types)
- **All**: Always adds `$type` (unnecessary overhead)
- **None**: Never adds `$type` (our original error)

**We use Auto** for best balance.

### **2. ReferenceLoopHandling.Ignore**

Prevents errors when objects reference each other:

```csharp
// Without ReferenceLoopHandling
User.Devices → Device.User → User.Devices → ... ∞

// With ReferenceLoopHandling.Ignore
User.Devices → Device.User → null (stops)
```

### **3. Navigation Properties**

Our entities have `[ForeignKey]` navigation properties:

```csharp
public class DeviceAlertEntity
{
    [ForeignKey(nameof(UserKeyField))]
    public User? User { get; set; }  // Navigation property
}
```

**ReferenceLoopHandling.Ignore** prevents these from causing infinite loops.

---

## 🧪 TESTING

### **Test Polymorphic Serialization:**

```csharp
// Create mixed list of alerts
var alerts = new List<DeviceAlertEntity>
{
    new UrlAlertEntity { Url = "http://test.com" },
    new RemoteAccessAlertEntity { ConnectionUrl = "rdp://server" }
};

// Serialize with type info
var json = JsonConvert.SerializeObject(alerts, new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.Auto,
    Formatting = Formatting.Indented
});

Console.WriteLine(json);
// Should show $type fields

// Deserialize
var deserialized = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, 
    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

// Check types
Console.WriteLine(deserialized[0].GetType().Name);  // "UrlAlertEntity"
Console.WriteLine(deserialized[1].GetType().Name);  // "RemoteAccessAlertEntity"
```

---

## 🎯 QUERIES THAT NEED THIS

### **Queries Returning Polymorphic Types:**

| Query | Returns | Why TypeNameHandling Needed |
|-------|---------|----------------------------|
| GetRecentAlertsQuery | `List<DeviceAlertEntity>` | Abstract class with subtypes |
| GetAllDevicesQuery | `List<UserDevice>` | Abstract class with subtypes |
| GetDevicesByUserQuery | `List<UserDevice>` | Abstract class with subtypes |

### **Queries NOT Needing This:**

| Query | Returns | Why TypeNameHandling NOT Needed |
|-------|---------|--------------------------------|
| GetDashboardStatsQuery | Primitive counts | No polymorphic types |
| GetUsersWithDeviceCountsQuery | `List<User>` | Concrete class |

**However**, we apply it to ALL queries for consistency and to handle navigation properties.

---

## 📊 EXAMPLE JSON OUTPUT

### **GetRecentAlertsQuery Response:**

```json
{
  "Success": true,
  "Message": "",
  "Alerts": [
    {
      "$type": "Common.Entities.UrlAlertEntity, Common",
      "Url": "http://phishing-site.com",
      "TrackerKeys": "[]",
      "IFrameDomains": "[]",
      "UserAgent": "Mozilla/5.0",
      "KeyField": "abc-123",
      "AlertType": "UrlAlert",
      "Priority": 1,
      "Timestamp": "2026-01-24T22:00:00Z",
      "DeviceUid": "PC-001",
      "UserKeyField": "user-456"
    },
    {
      "$type": "Common.Entities.RemoteAccessAlertEntity, Common",
      "RemoteAccessApp": 1,
      "ConnectionUrl": "rdp://server.com",
      "KeyField": "def-789",
      "AlertType": "RemoteAccess",
      "Priority": 2,
      "Timestamp": "2026-01-24T21:55:00Z",
      "DeviceUid": "PC-002"
    }
  ]
}
```

**Notice:** Each alert has `$type` field indicating concrete type!

---

## ✅ SUMMARY

**Problem:** Can't deserialize abstract types without knowing concrete type.

**Solution:** Use `TypeNameHandling.Auto` in JSON serialization.

**Where Applied:**
- CQRSGateway: When serializing responses
- CQRSClient: When deserializing responses

**Benefits:**
- Handles polymorphic types correctly
- Preserves concrete type information
- Prevents reference loops with navigation properties

**Settings Used:**
```csharp
new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.Auto,
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
}
```

This ensures proper serialization/deserialization of all polymorphic entities! ✅
