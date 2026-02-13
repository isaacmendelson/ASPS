# Discriminator Values Reference

## ❌ ERROR: "No discriminators matched the discriminator value 'UrlAlert'"

This error occurs when database records have incorrect discriminator values that don't match the EF Core configuration.

---

## ✅ CORRECT DISCRIMINATOR VALUES

### **AnalysisResults Table**

| Entity Type | Discriminator Value |
|-------------|---------------------|
| AnalysisResultContainer | `"AnalysisResult"` |
| UrlAnalysisResultContainer | `"UrlAnalysisResult"` ✅ |

**WRONG Values:**
- ❌ `"UrlAlert"` - This belongs to DeviceAlerts table!

### **DeviceAlerts Table**

| Entity Type | Discriminator Value |
|-------------|---------------------|
| RemoteAccessAlertEntity | `"RemoteAccess"` |
| UrlAlertEntity | `"Url"` |

**WRONG Values:**
- ❌ `"UrlAnalysisResult"` - This belongs to AnalysisResults table!

### **UserDevices Table**

| Entity Type | Discriminator Value |
|-------------|---------------------|
| PersonalComputer | `"PC"` |
| SmartPhone | `"Phone"` |

---

## 🔧 FIX THE ERROR

### **Quick Fix:**

```bash
mysql -u root -p ASPSBackend2DB < FIX-ANALYSISRESULTS-DISCRIMINATORS.sql
```

This will:
1. Show current discriminator values
2. Change `"UrlAlert"` → `"UrlAnalysisResult"`
3. Verify the fix

---

## 📋 Step-by-Step Fix

### **Step 1: Check Current State**

```bash
mysql -u root -p ASPSBackend2DB < CHECK-ALL-DISCRIMINATORS.sql
```

Look for incorrect values:
- AnalysisResults with `"UrlAlert"` ❌
- DeviceAlerts with `"UrlAnalysisResult"` ❌

### **Step 2: Fix AnalysisResults**

```bash
mysql -u root -p ASPSBackend2DB < FIX-ANALYSISRESULTS-DISCRIMINATORS.sql
```

### **Step 3: Restart ASPSBackend**

```bash
dotnet run --project ASPSBackend
```

ASView should now initialize without errors!

---

## 🔍 MANUAL FIX (SQL)

If you prefer to run commands manually:

```sql
USE ASPSBackend2DB;

-- Check current values
SELECT Discriminator, COUNT(*) 
FROM AnalysisResults 
GROUP BY Discriminator;

-- Fix incorrect values
UPDATE AnalysisResults
SET Discriminator = 'UrlAnalysisResult'
WHERE Discriminator = 'UrlAlert';

-- Verify
SELECT Discriminator, COUNT(*) 
FROM AnalysisResults 
GROUP BY Discriminator;
```

---

## 🎯 EF CORE CONFIGURATION

### **AppDbContext.cs - AnalysisResults**

```csharp
modelBuilder.Entity<AnalysisResultContainer>(entity =>
{
    entity.HasDiscriminator<string>("Discriminator")
        .HasValue<AnalysisResultContainer>("AnalysisResult")
        .HasValue<UrlAnalysisResultContainer>("UrlAnalysisResult");  // ← Expects this!
});
```

### **AppDbContext.cs - DeviceAlerts**

```csharp
modelBuilder.Entity<DeviceAlertEntity>(entity =>
{
    entity.HasDiscriminator<string>("Discriminator")
        .HasValue<RemoteAccessAlertEntity>("RemoteAccess")
        .HasValue<UrlAlertEntity>("Url");  // ← Not "UrlAlert"!
});
```

---

## ⚠️ WHY THIS HAPPENED

### **Root Cause:**

Likely one of these scenarios:

1. **Code Changed:** Discriminator value was changed in code but database wasn't updated
2. **Manual Insert:** Records inserted manually with wrong discriminator
3. **Migration Issue:** Old migration script used wrong value
4. **Copy/Paste Error:** Data copied from DeviceAlerts to AnalysisResults

### **Table-Per-Hierarchy (TPH):**

EF Core uses TPH for inheritance:
- Single table stores multiple entity types
- `Discriminator` column identifies entity type
- EF Core uses discriminator to deserialize to correct class

**When discriminator is wrong:**
- EF Core doesn't know which class to create
- Throws "No discriminators matched" error
- Query fails

---

## 🧪 VERIFY THE FIX

### **Test 1: Check Discriminators**

```sql
SELECT Discriminator, COUNT(*) as Count
FROM AnalysisResults
GROUP BY Discriminator;
```

Expected result:
```
Discriminator        | Count
--------------------|------
AnalysisResult      | XX
UrlAnalysisResult   | XX    ← Should be this, NOT "UrlAlert"
```

### **Test 2: Query Records**

```sql
SELECT `Key`, Discriminator, Timestamp
FROM AnalysisResults
WHERE Discriminator = 'UrlAnalysisResult'
LIMIT 5;
```

Should return records without errors.

### **Test 3: Start ASPSBackend**

```bash
dotnet run --project ASPSBackend
```

ASView should initialize without discriminator errors.

---

## 📊 ALL DISCRIMINATOR VALUES

### **Complete Reference Table**

| Table | Entity | Discriminator |
|-------|--------|---------------|
| **AnalysisResults** | AnalysisResultContainer | `"AnalysisResult"` |
| **AnalysisResults** | UrlAnalysisResultContainer | `"UrlAnalysisResult"` |
| **DeviceAlerts** | RemoteAccessAlertEntity | `"RemoteAccess"` |
| **DeviceAlerts** | UrlAlertEntity | `"Url"` |
| **UserDevices** | PersonalComputer | `"PC"` |
| **UserDevices** | SmartPhone | `"Phone"` |

---

## 🔄 PREVENT FUTURE ISSUES

### **1. Don't Manually Edit Discriminators**

```sql
-- ❌ BAD - Don't do this
UPDATE AnalysisResults SET Discriminator = 'SomeValue';

-- ✅ GOOD - Let EF Core handle it
// Use C# code to insert records
```

### **2. Use Correct Entity Types**

```csharp
// ✅ Correct - EF sets discriminator automatically
var result = new UrlAnalysisResultContainer
{
    // EF Core will set Discriminator = "UrlAnalysisResult"
};
await context.AnalysisResults.AddAsync(result);
```

### **3. Check After Migrations**

After any migration, verify discriminators:
```bash
mysql -u root -p ASPSBackend2DB < CHECK-ALL-DISCRIMINATORS.sql
```

---

## 🆘 TROUBLESHOOTING

### **Error: "No discriminators matched the discriminator value 'X'"**

**Solution:**
1. Identify which table has the error (from stack trace)
2. Check discriminator values in that table
3. Update incorrect values to match EF Core configuration

**Common Fixes:**
```sql
-- Fix AnalysisResults
UPDATE AnalysisResults SET Discriminator = 'UrlAnalysisResult' 
WHERE Discriminator = 'UrlAlert';

-- Fix DeviceAlerts
UPDATE DeviceAlerts SET Discriminator = 'Url' 
WHERE Discriminator = 'UrlAlert';

-- Fix UserDevices
UPDATE UserDevices SET Discriminator = 'PC' 
WHERE Discriminator = 'PersonalComputer';

UPDATE UserDevices SET Discriminator = 'Phone' 
WHERE Discriminator = 'SmartPhone';
```

### **Multiple Wrong Values**

Run the comprehensive check:
```bash
mysql -u root -p ASPSBackend2DB < CHECK-ALL-DISCRIMINATORS.sql
```

Fix each incorrect value based on the reference table above.

---

## ✅ SUMMARY

**Problem:** AnalysisResults table has records with `Discriminator = "UrlAlert"` but EF Core expects `"UrlAnalysisResult"`.

**Solution:** Update discriminator values in database.

**Command:**
```bash
mysql -u root -p ASPSBackend2DB < FIX-ANALYSISRESULTS-DISCRIMINATORS.sql
```

**Prevention:** Let EF Core manage discriminators - don't manually set them.

**Verification:**
```bash
mysql -u root -p ASPSBackend2DB < CHECK-ALL-DISCRIMINATORS.sql
```

All discriminators should match the reference table! ✅
