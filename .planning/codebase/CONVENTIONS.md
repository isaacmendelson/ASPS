# Coding Conventions

**Analysis Date:** 2026-02-16

## Naming Patterns

**Files:**
- PascalCase for class files: `UserCommandHandlers.cs`, `AppDbContext.cs`, `RemoteAccessAlert.cs`
- PascalCase for enum files: `Enumerations.cs`, `WebsiteType.cs`
- Generally one class per file, except CQRS commands/queries which may group related items

**Classes & Types:**
- PascalCase for all public classes: `UserCommandHandlers`, `DeviceAlertEntity`, `RealTimeAlertListener`
- PascalCase for interfaces: `IRepository<T>`, `IDomainEventHandler`, `IIndicator`
- PascalCase for enums: `DeviceType`, `AlertFlagStatus`, `UserRole`

**Properties:**
- PascalCase for all public properties: `FirstName`, `KeyField`, `DateCreated`
- Backing fields use camelCase with underscore prefix for private: `_userRepository`, `_serviceProvider`, `_logger`, `_isRunning`
- Auto-properties used extensively: `public string Email { get; set; }`

**Methods:**
- PascalCase for public methods: `GetAllAsync()`, `HandleAsync()`, `AddAsync()`
- Async methods suffixed with `Async`: `HandleAsync()`, `GetByKeyAsync()`, `ProcessMessagesAsync()`
- Handler methods follow pattern: `HandleAsync(CommandOrQuery)` or `Handle(IDomainEvent)`
- Private methods: PascalCase: `GetDbKey()`, `ProcessDeviceAlertReceived()`

**Variables & Parameters:**
- camelCase for local variables: `user`, `command`, `serviceProvider`, `allItems`
- camelCase for method parameters: `command`, `key`, `options`

**Constants:**
- Not commonly used in codebase; enums preferred for type-safe constants
- String constants appear inline within methods

## Code Style

**Formatting:**
- .NET 8.0 with nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- No strict linter configuration found; formatting appears ad-hoc
- Indentation: 4 spaces (standard C# convention)

**Linting:**
- No .editorconfig or explicit linting rules detected
- Relies on Visual Studio defaults and developer discipline
- Code follows general C# style guidelines

**Imports Organization:**
Order in source files:
1. System namespaces (`using System;`, `using System.Linq;`)
2. Microsoft namespaces (`using Microsoft.EntityFrameworkCore;`)
3. Third-party namespaces (`using NetMQ;`, `using Newtonsoft.Json;`)
4. Internal project namespaces (`using Business.Data.EF;`, `using Common.Entities;`)

Example from `Program.cs`:
```csharp
using Business.Data.EF;
using Business.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
```

**Namespace Structure:**
- Follows directory structure: Directory → Namespace
- Example: `ASPSBackend14_J/Business/Handlers/` → `namespace Business.Handlers;`
- Top-level namespace matches assembly: ASPSBackend, Business, Common, Interface, WebApi

## Error Handling

**Exception Patterns:**
- Try-catch blocks standard in all handlers and repositories
- Generic exception catching common: `catch (Exception ex)`
- Exceptions logged via ILogger: `_logger.LogError(ex, "Error message")`
- CommandResult/QueryResult objects used to communicate errors without throwing:
  ```csharp
  catch (Exception ex)
  {
      return new CreateUserCommandResult
      {
          Success = false,
          Message = $"Error creating user: {ex.Message}"
      };
  }
  ```

**Logging:**
- Uses Microsoft.Extensions.Logging
- ILogger<T> injected in constructors: `ILogger<NetMQMessageProcessor>`
- Log levels used: LogInformation, LogError, LogTrace
- Debug console output via Console.WriteLine for low-level tracing
- Examples from `CommandQueryHandlers.cs`:
  ```csharp
  Console.WriteLine("DEBUG: GetAllUsersQuery handler called");
  Console.WriteLine($"DEBUG: GetAllAsync returned {users.Count()} users");
  ```

**Null Handling:**
- Null-conditional operators used: `_responseSocket?.SendFrame()`, `user?.Key`
- Null coalescing for defaults: `command.KeycloakUserId ?? Guid.NewGuid().ToString()`
- Explicit null checks common: `if (entity != null)`, `if (user == null)`

## Validation

**Input Validation:**
- Minimal explicit validation in handlers
- Reliance on Entity Framework validation (data annotations)
- Type-safe Key objects prevent many invalid inputs

**Domain Validation:**
- Business logic constraints implemented in Entity classes
- Example: `DeviceAlertEntity` has required properties like `AlertType`, `Priority`
- IsDeleted flags used for soft deletes instead of database removal

## Comments

**When to Comment:**
- XML documentation (///`) used for public APIs and service classes
- Example from `TokenStore.cs`:
  ```csharp
  /// <summary>
  /// In-memory store for device authentication tokens.
  /// Thread-safe via ConcurrentDictionary, keyed by DeviceUid.
  /// </summary>
  public class TokenStore
  ```
- Inline comments used sparingly for non-obvious logic
- Examples in `Entity.cs`:
  ```csharp
  // Don't rename to "keyField" so that EF will not recognize it as the backing field
  private string myKeyField = string.Empty;
  ```

**JSDoc/TSDoc:**
- Not applicable (C# project)
- XML documentation tags used instead: `<summary>`, `<param>`, `<returns>`

## Function Design

**Size Guidelines:**
- Handler methods typically 20-40 lines
- Private helper methods used for complex logic
- Example: `ProcessDeviceAlertReceived()` extracted from `HandleDeviceAlertReceived()` in `ASView.cs`

**Parameters:**
- Explicit parameter pattern common: Command/Query objects passed to handlers
- Example: `HandleAsync(CreateUserCommand command)`
- Dependency injection via constructor, not method parameters

**Return Values:**
- Command handlers return CommandResult subclasses with Success/Message properties
- Query handlers return QueryResult subclasses containing Data
- Async methods return Task or Task<T>
- Example:
  ```csharp
  public async Task<CreateUserCommandResult> HandleAsync(CreateUserCommand command)
  ```

## Module Design

**Exports:**
- Public classes explicitly defined; no wildcard exports
- Interfaces defined in separate files: `IRepository.cs`, `IDomainEvent.cs`
- Internal static helper methods for cross-module access: `Entity.GetDbKey()`

**Barrel Files:**
- Not used; each file contains single class or tightly related classes
- CQRS commands/queries grouped in same file by domain:
  - `UserCommands.cs`: CreateUserCommand, UpdateUserCommand, DeleteUserCommand
  - `UserQueries.cs`: GetAllUsersQuery, GetUserByKeyQuery

## CQRS Pattern

**Command Format:**
- Inherit from `Command` base class
- Pair with dedicated `CommandResult` subclass
- Handler method: `public async Task<ResultType> HandleAsync(CommandType command)`

**Query Format:**
- Inherit from `Query` base class
- Pair with dedicated `QueryResult` subclass
- Handler method: `public async Task<ResultType> HandleAsync(QueryType query)`

Example command/result pair:
```csharp
public class CreateUserCommand : Command
{
    public string KeycloakUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}

public class CreateUserCommandResult : CommandResult
{
    public Key? UserKey { get; set; }
}
```

## Async/Await

**Pattern:**
- All repository methods are async: `GetByKeyAsync()`, `AddAsync()`, `UpdateAsync()`
- All handler methods are async
- Background tasks use Task.Run for fire-and-forget: `Task.Run(() => ProcessDeviceAlertReceived(alertEvent))`
- Proper await of database operations to avoid sync-over-async

---

*Convention analysis: 2026-02-16*
