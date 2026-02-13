# KnownPhishingWebsite Entity Implementation

## Overview
Added `KnownPhishingWebsite` entity to store known phishing URLs for use in URL analysis. The entity uses **INT AUTO_INCREMENT** keys for performance and includes domain extraction for fast lookup.

---

## Components Created

### **1. Entity: KnownPhishingWebsite**
**File:** `Common/Entities/KnownPhishingWebsite.cs`

**Properties:**
- `Id` (int) - Primary key, AUTO_INCREMENT (mapped to "Key" column)
- `Url` (string) - Full phishing URL (TEXT)
- `Domain` (string) - Extracted domain (VARCHAR 255, indexed)
- `DateCreated` (DateTime) - Creation timestamp
- `DateDeleted` (DateTime?) - Soft delete timestamp
- `Source` (string?) - Source of URL (VARCHAR 100)

**Key Features:**
- ✅ Uses INT AUTO_INCREMENT for performance
- ✅ Domain extracted and stored for fast lookups
- ✅ Soft delete support
- ✅ Static `GetDomainFromUrl()` method
- ✅ `IsActive` property for checking deletion status

**Example Usage:**
```csharp
var phishingUrl = new KnownPhishingWebsite(
    "http://phishing-site.com/login",
    "PhishTank"
);

// Domain is automatically extracted
Console.WriteLine(phishingUrl.Domain); // "phishing-site.com"
```

---

### **2. Repository Interface: IKnownPhishingWebsiteRepository**
**File:** `Interface/Repositories/IEntityRepositories.cs`

**Methods:**
```csharp
Task<KnownPhishingWebsite?> GetByIdAsync(int id);
Task<IEnumerable<KnownPhishingWebsite>> GetAllActiveAsync();
Task<KnownPhishingWebsite?> GetByUrlAsync(string url);
Task<IEnumerable<KnownPhishingWebsite>> GetByDomainAsync(string domain);
Task<bool> IsPhishingUrlAsync(string url);
Task<bool> IsPhishingDomainAsync(string domain);
Task<int> AddAsync(KnownPhishingWebsite website);
Task<int> AddRangeAsync(IEnumerable<KnownPhishingWebsite> websites);
Task UpdateAsync(KnownPhishingWebsite website);
Task DeleteAsync(int id);
Task<int> GetCountAsync();
```

---

### **3. Repository Implementation: KnownPhishingWebsiteRepository**
**File:** `Business/Data/EF/Repositories/KnownPhishingWebsiteRepository.cs`

**Key Methods:**

**Check if URL is Phishing:**
```csharp
var isPhishing = await repository.IsPhishingUrlAsync("http://suspicious-site.com");
```

**Check if Domain is Phishing:**
```csharp
var isPhishing = await repository.IsPhishingDomainAsync("suspicious-site.com");
```

**Get All Phishing URLs for Domain:**
```csharp
var phishingUrls = await repository.GetByDomainAsync("suspicious-site.com");
```

**Bulk Import:**
```csharp
var urls = new List<KnownPhishingWebsite>
{
    new("http://phish1.com", "PhishTank"),
    new("http://phish2.com", "OpenPhish")
};
await repository.AddRangeAsync(urls);
```

---

### **4. Database Configuration**
**File:** `Business/Data/EF/AppDbContext.cs`

**DbSet Added:**
```csharp
public DbSet<KnownPhishingWebsite> KnownPhishingWebsites { get; set; }
```

**Entity Configuration:**
```csharp
modelBuilder.Entity<KnownPhishingWebsite>(entity =>
{
    entity.ToTable("KnownPhishingWebsites");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id)
        .HasColumnName("Key")
        .ValueGeneratedOnAdd();
    
    entity.Property(e => e.Url)
        .IsRequired()
        .HasColumnType("TEXT");
    
    entity.Property(e => e.Domain)
        .IsRequired()
        .HasMaxLength(255);
    
    entity.Property(e => e.Source)
        .HasMaxLength(100);
    
    // Indexes
    entity.HasIndex(e => e.Domain);
    entity.HasIndex(e => e.DateDeleted);
    entity.HasIndex(e => e.Url);
});
```

---

### **5. Dependency Injection**
**File:** `ASPSBackend/Program.cs`

**Registration:**
```csharp
services.AddScoped<IKnownPhishingWebsiteRepository, KnownPhishingWebsiteRepository>();
```

---

## Database Table

### **Table: KnownPhishingWebsites**

```sql
CREATE TABLE KnownPhishingWebsites (
    `Key` INT AUTO_INCREMENT PRIMARY KEY,
    Url TEXT NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    DateCreated DATETIME NOT NULL,
    DateDeleted DATETIME NULL,
    Source VARCHAR(100) NULL,
    
    INDEX idx_domain (Domain),
    INDEX idx_date_deleted (DateDeleted),
    INDEX idx_url (Url(255))
);
```

**Columns:**
- `Key` - INT AUTO_INCREMENT primary key
- `Url` - TEXT, full URL
- `Domain` - VARCHAR(255), extracted domain (indexed for fast lookup)
- `DateCreated` - DATETIME, creation timestamp
- `DateDeleted` - DATETIME NULL, soft delete
- `Source` - VARCHAR(100) NULL, source identifier

**Indexes:**
- `idx_domain` - Fast domain lookups
- `idx_date_deleted` - Filter out deleted entries
- `idx_url` - Fast URL lookups (first 255 chars)

---

## Migration & Setup

### **1. Create Table:**
```bash
mysql -u root -p ASPSBackend2DB < CREATE-KNOWN-PHISHING-WEBSITES-TABLE.sql
```

**Or execute manually:**
```sql
USE ASPSBackend2DB;
SOURCE CREATE-KNOWN-PHISHING-WEBSITES-TABLE.sql;
```

### **2. Verify Table:**
```sql
DESCRIBE KnownPhishingWebsites;
SELECT COUNT(*) FROM KnownPhishingWebsites;
```

---

## CSV Import

### **Python Import Script**
**File:** `import-phishing-urls.py`

**Requirements:**
```bash
pip install mysql-connector-python
```

**CSV Format (Option 1 - URL only):**
```csv
url
http://phishing-site.com
https://scam-site.com
```

**CSV Format (Option 2 - URL + Source):**
```csv
url,source
http://phishing-site.com,PhishTank
https://scam-site.com,OpenPhish
```

**Usage:**
```bash
python import-phishing-urls.py phishing_urls.csv
```

**What It Does:**
1. ✅ Reads CSV file
2. ✅ Extracts domain from each URL
3. ✅ Checks for duplicates
4. ✅ Inserts into database
5. ✅ Shows progress every 100 records
6. ✅ Reports statistics at end

**Example Output:**
```
======================================================================
Phishing URL Import Utility
======================================================================

CSV File: phishing_urls.csv
Database: ASPSBackend2DB
Host: localhost:3306

Connecting to database...
✓ Connected successfully

Reading CSV file: phishing_urls.csv
✓ CSV Headers: ['url', 'source']

Processing URLs...
----------------------------------------------------------------------
  Imported: 100
  Imported: 200
  Imported: 300
----------------------------------------------------------------------

✓ Import completed!
  Imported: 325
  Skipped:  15
  Errors:   0

  Total active phishing URLs in database: 325

======================================================================
```

---

## Usage Examples

### **In URL Analyzer:**

```csharp
public class UDUrlAnalyzer : ISpecificAnalyzer
{
    private readonly IKnownPhishingWebsiteRepository _phishingRepo;
    
    public async Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts)
    {
        if (alert is UrlAlert urlAlert)
        {
            // Check if URL is known phishing
            var isPhishing = await _phishingRepo.IsPhishingUrlAsync(urlAlert.Url);
            
            if (isPhishing)
            {
                return new AnalyzerResult(
                    Severity.Critical,
                    "Known phishing URL detected!",
                    indicators,
                    new Dictionary<string, object>
                    {
                        ["is_known_phishing"] = true,
                        ["url"] = urlAlert.Url
                    }
                );
            }
            
            // Also check domain
            var domain = KnownPhishingWebsite.GetDomainFromUrl(urlAlert.Url);
            var isDomainPhishing = await _phishingRepo.IsPhishingDomainAsync(domain);
            
            if (isDomainPhishing)
            {
                return new AnalyzerResult(
                    Severity.Critical,
                    "Known phishing domain detected!",
                    indicators,
                    new Dictionary<string, object>
                    {
                        ["is_known_phishing_domain"] = true,
                        ["domain"] = domain
                    }
                );
            }
        }
    }
}
```

### **Add Single URL:**
```csharp
var phishingUrl = new KnownPhishingWebsite(
    "http://fake-bank-login.com",
    "Manual Report"
);
await _phishingRepo.AddAsync(phishingUrl);
```

### **Bulk Add:**
```csharp
var urls = new List<KnownPhishingWebsite>
{
    new("http://phish1.com", "PhishTank"),
    new("http://phish2.com", "OpenPhish"),
    new("http://phish3.com", "PhishTank")
};
await _phishingRepo.AddRangeAsync(urls);
```

### **Check URL:**
```csharp
var url = "http://suspicious-site.com";
var isPhishing = await _phishingRepo.IsPhishingUrlAsync(url);

if (isPhishing)
{
    Console.WriteLine("⚠️ WARNING: Known phishing URL!");
}
```

### **Get All for Domain:**
```csharp
var domain = "suspicious-site.com";
var phishingUrls = await _phishingRepo.GetByDomainAsync(domain);

Console.WriteLine($"Found {phishingUrls.Count()} phishing URLs for domain {domain}");
foreach (var url in phishingUrls)
{
    Console.WriteLine($"  - {url.Url} (Source: {url.Source})");
}
```

### **Soft Delete:**
```csharp
await _phishingRepo.DeleteAsync(123); // Marks as deleted
```

### **Get Statistics:**
```csharp
var count = await _phishingRepo.GetCountAsync();
Console.WriteLine($"Total active phishing URLs: {count}");
```

---

## Performance Considerations

### **Indexes:**
- ✅ `Domain` indexed for fast `IsPhishingDomainAsync()` lookups
- ✅ `Url` indexed for fast `IsPhishingUrlAsync()` lookups
- ✅ `DateDeleted` indexed for filtering active entries

### **Query Performance:**
```sql
-- Fast lookup by domain (uses index)
SELECT COUNT(*) FROM KnownPhishingWebsites 
WHERE Domain = 'suspicious-site.com' 
AND DateDeleted IS NULL;

-- Fast lookup by URL (uses index)
SELECT * FROM KnownPhishingWebsites 
WHERE Url = 'http://phishing-site.com' 
AND DateDeleted IS NULL;
```

### **Expected Performance:**
- Domain check: <1ms
- URL check: <1ms
- Bulk insert (1000 records): <500ms

---

## Future Enhancements

1. **Automatic Updates:**
   - Schedule job to fetch from PhishTank API
   - Schedule job to fetch from OpenPhish feed
   - Update database daily

2. **Expiration:**
   - Add `ExpiresAt` column
   - Automatically expire old entries

3. **Confidence Score:**
   - Add `ConfidenceScore` (0-100)
   - Weight analysis based on confidence

4. **URL Patterns:**
   - Store regex patterns for fuzzy matching
   - Match similar URLs

5. **Statistics:**
   - Track hit count per URL
   - Track false positive rate

---

## Testing

### **1. Create Test Data:**
```sql
INSERT INTO KnownPhishingWebsites (Url, Domain, DateCreated, Source)
VALUES 
    ('http://fake-paypal.com/login', 'fake-paypal.com', UTC_TIMESTAMP(), 'Test'),
    ('http://fake-bank.com/verify', 'fake-bank.com', UTC_TIMESTAMP(), 'Test'),
    ('http://scam-site.com/offer', 'scam-site.com', UTC_TIMESTAMP(), 'Test');
```

### **2. Test Repository Methods:**
```csharp
// Test IsPhishingUrlAsync
var isPhishing = await repo.IsPhishingUrlAsync("http://fake-paypal.com/login");
Assert.IsTrue(isPhishing);

// Test IsPhishingDomainAsync
var isDomainPhishing = await repo.IsPhishingDomainAsync("fake-paypal.com");
Assert.IsTrue(isDomainPhishing);

// Test GetByDomainAsync
var urls = await repo.GetByDomainAsync("fake-paypal.com");
Assert.AreEqual(1, urls.Count());

// Test soft delete
await repo.DeleteAsync(1);
var deleted = await repo.GetByIdAsync(1);
Assert.IsNull(deleted); // Should not return deleted records
```

### **3. Clean Up Test Data:**
```sql
DELETE FROM KnownPhishingWebsites WHERE Source = 'Test';
```

---

## Files Created/Modified

### **Created:**
1. `Common/Entities/KnownPhishingWebsite.cs` - Entity class
2. `Business/Data/EF/Repositories/KnownPhishingWebsiteRepository.cs` - Repository implementation
3. `CREATE-KNOWN-PHISHING-WEBSITES-TABLE.sql` - Table creation script
4. `import-phishing-urls.py` - CSV import utility
5. `KNOWN-PHISHING-WEBSITE-IMPLEMENTATION.md` - This documentation

### **Modified:**
1. `Interface/Repositories/IEntityRepositories.cs` - Added interface
2. `Business/Data/EF/AppDbContext.cs` - Added DbSet and configuration
3. `ASPSBackend/Program.cs` - Registered repository

---

## Summary

✅ **Entity created** with INT AUTO_INCREMENT key
✅ **Domain extraction** for fast lookups
✅ **Repository pattern** with full CRUD operations
✅ **Database table** with optimized indexes
✅ **Migration script** ready to run
✅ **CSV import utility** for bulk loading
✅ **Soft delete** support
✅ **Performance optimized** with proper indexing
✅ **Ready for integration** with URL analyzers

**Next Steps:**
1. Run migration script to create table
2. Import your CSV file with phishing URLs
3. Integrate with UDUrlAnalyzer for real-time checking

**Ready to use!** 🚀
