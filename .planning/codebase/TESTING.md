# Testing Patterns

**Analysis Date:** 2026-02-16

## Test Framework

**Runner:**
- Not formally configured - no xUnit, NUnit, or MSTest project detected
- No dedicated test projects (*.Tests.csproj) in solution structure

**Assertion Library:**
- Not applicable - formal testing framework not in use

**Test Infrastructure:**
- Two manual test files at solution root:
  - `DbContextTest.cs` - Manual database connection verification
  - `TestDatabaseConnection.cs` - Connection string testing
  - `TEST-RUNTIME.cs` - Runtime verification script

**Run Commands:**
- Manual testing only via Console application execution
- No automated test runner configured
- Tests executed by running standalone .cs files directly

## Test Structure

**Manual Tests Present:**

**`DbContextTest.cs`** (`c:\Jobs\ASPS\Software/ASPSBackend14_J/DbContextTest.cs`):
```csharp
class Program
{
    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();

        try
        {
            Console.WriteLine("[TEST] Attempting to create DbContext...");
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Console.WriteLine("[TEST] ✓ DbContext created successfully!");
            var canConnect = context.Database.CanConnect();
            Console.WriteLine($"[TEST] ✓ Database connection: {canConnect}");

            Console.WriteLine("[TEST] ✓✓✓ ALL TESTS PASSED ✓✓✓");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[TEST] ✗✗✗ FAILURE ✗✗✗");
            Console.WriteLine($"Message: {ex.Message}");
        }
    }
}
```

**Patterns:**
- Setup: ServiceCollection with ConfigurationBuilder for configuration loading
- DbContext creation with dependency injection
- Database connectivity check via `context.Database.CanConnect()`
- Entity type introspection via `context.Model.GetEntityTypes()`
- Property mapping verification via `prop.FindTypeMapping()`
- Teardown: Explicit Exception handling with detailed error reporting

**Testing Approach:**
- Verification tests - check system state after operations
- No isolation - tests connect to actual database
- Comprehensive error reporting with InnerException handling

## Mocking

**Framework:**
- Not applicable - no formal mocking framework in use
- Manual test stubs created where needed

**Current State:**
- Repositories use actual database via EntityFramework DbContext
- Views (like `ASView`) load real data via repositories
- No test doubles or mock implementations detected

## Fixtures and Factories

**Test Data:**
- Not formally structured
- Manual test files create fresh ServiceProvider and DbContext
- Database-driven data sourced from live MySQL connection string in `appsettings.json`

**Location:**
- Manual test files at project root: `DbContextTest.cs`, `TestDatabaseConnection.cs`
- No dedicated fixtures directory

## Coverage

**Requirements:**
- No test coverage targets enforced
- Coverage tracking not configured

**Manual Verification:**
- Console output used to verify results
- Example from `DbContextTest.cs`:
  ```csharp
  Console.WriteLine($"Total records loaded from database: {allItems.Count}");
  foreach (var item in allItems)
  {
      Console.WriteLine($"  Item: Key={item.Key}, IsDeleted={item.IsDeleted}");
  }
  Console.WriteLine($"Records after IsDeleted filter: {filtered.Count}");
  ```

## Test Types

**Integration Tests (Manual):**
- Scope: Full stack with real database connection
- Approach: Create DbContext, load data, verify results
- Files: `DbContextTest.cs` - Comprehensive integration test
- Database operations tested directly via repositories
- EF Core logging enabled for SQL verification:
  ```csharp
  options.LogTo(Console.WriteLine, LogLevel.Trace)
      .EnableSensitiveDataLogging()
      .EnableDetailedErrors();
  ```

**Unit Tests:**
- Not formally implemented
- Debugging via Console.WriteLine statements in code instead:
  - `Repository<T>.GetAllAsync()` has extensive console logging: lines 28-60
  - `UserQueryHandlers.HandleAsync()` has DEBUG output: lines 115-128
  - Repository queries print debug info before/after operations

**E2E Tests:**
- Not implemented
- Manual verification via WebApi Pages and Controllers

## Verification Patterns

**Database Verification:**
From `Repository.cs` GetAllAsync():
```csharp
public virtual async Task<IEnumerable<T>> GetAllAsync()
{
    try
    {
        Console.WriteLine($"=== Repository<{typeof(T).Name}>.GetAllAsync START ===");

        var allItems = await _context.Set<T>()
            .AsNoTracking()
            .ToListAsync();

        Console.WriteLine($"Total records loaded from database: {allItems.Count}");

        foreach (var item in allItems)
        {
            Console.WriteLine($"  Item: Key={item.Key}, IsDeleted={item.IsDeleted}");
        }

        var filtered = allItems.Where(e => !e.IsDeleted).ToList();
        Console.WriteLine($"Records after IsDeleted filter: {filtered.Count}");

        return filtered;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR in Repository<{typeof(T).Name}>.GetAllAsync");
        Console.WriteLine($"Message: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        return new List<T>();
    }
}
```

**Pattern:** Extensive logging for debugging instead of assertions

## Error Testing

**Current Approach:**
- Exception types logged with full details
- InnerException and stack traces captured
- No try-catch assertions; exceptions bubble up

Example from `DbContextTest.cs`:
```csharp
catch (Exception ex)
{
    Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
        Console.WriteLine($"Inner Stack:\n{ex.InnerException.StackTrace}");
    }
}
```

## Testing Anti-Patterns

**Identified Issues:**
1. **Console-based Logging Instead of Unit Tests:** Code relies on `Console.WriteLine()` for debugging rather than formal assertions
2. **No Test Isolation:** Manual tests connect to live database; no test data cleanup
3. **Missing Failure Cases:** No tests for error paths or exception scenarios
4. **Debug Statements in Production Code:** DEBUG output left in handlers:
   - `CommandQueryHandlers.cs` line 115: `Console.WriteLine("DEBUG: GetAllUsersQuery handler called");`
   - `EntityRepositories.cs` line 41: `Console.WriteLine($"DEBUG: Total users in DB: {allUsers.Count}");`

## Testing Recommendations

**For Future Implementation:**
1. Introduce xUnit or NUnit test framework via new test project
2. Create repository mocks for unit testing handlers
3. Implement proper test data setup/teardown patterns
4. Move console debugging to configurable logging (ILogger)
5. Create integration test suite with test database
6. Add assertions for error conditions in handlers

## Dependency Injection in Tests

**Current Pattern:**
- ServiceCollection used to build DI container
- Repositories injected via constructor
- No service locator anti-pattern

Manual test example:
```csharp
var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options => { ... });
var serviceProvider = services.BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

---

*Testing analysis: 2026-02-16*
