# Coding Conventions

**Analysis Date:** 2026-02-13

## Project-Specific Conventions

This is a multi-language, multi-project codebase with distinct conventions per subproject.

---

## C# Backend (ASPSBackend14_J)

### Naming Patterns

**Files:**
- PascalCase for all files: `Program.cs`, `UserCommands.cs`, `AppDbContext.cs`
- Match class name: `UserCommandHandlers.cs` contains `UserCommandHandlers` class

**Classes:**
- PascalCase: `UserCommandHandlers`, `AppDbContext`, `NetMQMessageProcessor`
- Prefix interfaces with `I`: `IUserRepository`, `IRepository<T>`
- Command/Query suffix pattern: `CreateUserCommand`, `GetAllUsersQuery`, `CreateUserCommandResult`

**Methods:**
- PascalCase: `HandleAsync()`, `GetByKeyAsync()`, `AddAsync()`
- Async suffix for async methods: `GetAllAsync()`, `CreateHostBuilder()`

**Properties:**
- PascalCase: `FirstName`, `LastName`, `DeviceUid`, `KeyField`

**Variables:**
- camelCase for locals: `var connectionString`, `var user`, `var device`
- Private fields with underscore prefix: `_context`, `_userRepository`, `_asView`

**Namespaces:**
- Match directory structure: `Business.Commands`, `Common.Entities`, `Business.Data.EF.Repositories`

### Code Style

**Formatting:**
- No explicit formatter detected
- 4-space indentation (consistent across files)
- Opening braces on same line for methods/classes
- Var keyword preferred over explicit types for local variables

**Linting:**
- No linter configuration detected

**Null handling:**
- Nullable reference types enabled: `User?`, `Tag?`
- Null-conditional operators: `tag is not null`, `error ?? new ErrorMessage(...)`

### Import Organization

**Order:**
1. System namespaces
2. Microsoft namespaces
3. Third-party packages
4. Project namespaces (Business, Common, Interface)

**Example:**
```csharp
using Business.Data.EF;
using Business.Handlers;
using Business.Messaging;
using Common.Entities;
using Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
```

**Path Aliases:**
- None - fully qualified namespaces

### Error Handling

**Patterns:**
- Try-catch in handlers with Result objects:
```csharp
try {
    // Operation
    return new CommandResult { Success = true, Message = "..." };
} catch (Exception ex) {
    return new CommandResult { Success = false, Message = $"Error: {ex.Message}" };
}
```

- Custom domain exceptions with ErrorMessage:
```csharp
public class DomainException : Exception {
    public ErrorMessage Reason { get; private set; }
    public HttpStatusCode Code { get; private set; }
}
```

- Repository error handling with verbose Console logging:
```csharp
catch (Exception ex) {
    Console.WriteLine($"ERROR in Repository: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
    return new List<T>();
}
```

### Logging

**Framework:** Console.WriteLine (no structured logging library)

**Patterns:**
```csharp
Console.WriteLine("========================================");
Console.WriteLine("✓ ASView started");
Console.WriteLine($"DEBUG: GetAllAsync returned {users.Count()} users");
Console.WriteLine($"=== Repository<{typeof(T).Name}>.GetAllAsync START ===");
```

**When to Log:**
- Application startup/shutdown events
- Service initialization
- Database operations (verbose in Repository)
- Error conditions with full stack traces

### Comments

**When to Comment:**
- Complex CQRS/DDD patterns not immediately obvious
- Database mapping edge cases
- TODO items for unimplemented features

**Examples:**
```csharp
// Navigation properties removed - use repositories to fetch related data
// Generate new GUID if not set
// TODO: Implement email notification
```

**JSDoc/TSDoc:** Not used in C#

### Function Design

**Size:** Methods range 10-50 lines, handlers typically 20-30 lines

**Parameters:**
- Commands/Queries passed as single object parameter
- Dependency injection via constructor

**Return Values:**
- Async methods return `Task<T>` or `Task`
- Command handlers return typed Result objects: `CreateUserCommandResult`
- Repository methods return `T?`, `IEnumerable<T>`, or `Task<T>`

### Module Design

**Exports:**
- Public classes exported implicitly by namespace
- Internal implementation details kept private

**Architecture:**
- CQRS pattern: Commands and Queries in separate files
- Repository pattern: Generic `Repository<T>` base class
- Dependency injection: Services registered in `Program.cs`

---

## Python Desktop App (apps/desktop/win)

### Naming Patterns

**Files:**
- snake_case: `zmq_client.py`, `auth_manager.py`, `cache_manager.py`
- Test files: `diag_ws_test.py`, `diag_zmq_test.py`

**Classes:**
- PascalCase: `ZMQClient`, `AntiScamApp`, `CacheManager`, `Container`

**Functions:**
- snake_case: `send_url_alert()`, `get_or_create_device_id()`, `_setup_callbacks()`
- Private methods with leading underscore: `_handle_extension_message()`, `_on_dashboard()`
- Async functions: `async def start(self):`

**Variables:**
- snake_case: `device_id`, `notification_thread`, `risk_score`
- Constants in UPPER_SNAKE_CASE: `BACKEND_HOST`, `BACKEND_REQ_PORT`, `VERSION`

**Enums:**
- IntEnum classes: `DeviceType`, `RemoteAccessApp`, `Priority`
- PascalCase enum members: `DeviceType.PersonalComputer`, `Priority.Medium`

### Code Style

**Formatting:**
- No explicit formatter (no .prettierrc/.black config detected)
- 4-space indentation
- Double quotes for strings: `"AntiScam Desktop"`
- F-strings for formatting: `f"[ZMQ] Server: tcp://{self.host}:{self.port}"`

**Linting:**
- No .pylintrc or .flake8 detected

**Type hints:**
- Used extensively: `def send_alert(self, alert: Dict[str, Any]) -> Optional[Dict[str, Any]]:`
- Typing imports: `from typing import Optional, Dict, Any`

### Import Organization

**Order:**
1. Standard library imports
2. Third-party packages
3. Local application imports

**Example:**
```python
import asyncio
import json
import logging
import sys
import os

from config import VERSION, BACKEND_HOST
from core.container import Container
from hardware_id import get_or_create_device_id
```

**Path Aliases:**
- `sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))` for src directory

### Error Handling

**Patterns:**
- Try-except with optional return values:
```python
try:
    response = self.send_alert(alert)
    return response
except zmq.Again:
    print(f"[ZMQ] WARNING: Timeout after {self.timeout}ms")
    return None
except Exception as e:
    print(f"[ZMQ] ERROR: {e}")
    logger.error(f"ZMQ send error: {e}")
    return None
```

- Graceful degradation for optional features:
```python
except Exception as e:
    self._notification_connected = False
    print(f"[NOTIFY] Failed to start: {e}")
```

### Logging

**Framework:** Python `logging` module

**Setup:**
```python
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)
```

**Patterns:**
- `logger.info()`, `logger.error()`, `logger.debug()` for structured logging
- `print()` with prefixes for user-facing console output:
```python
print(f"[ZMQ] Client initialized")
print(f"[STARTUP] Authenticated!")
print("=" * 70)  # Visual separators
```

### Comments

**When to Comment:**
- Module docstrings at file top:
```python
"""
AntiScam Desktop App - ZeroMQ Client
ZMQ REQ/REP client for backend server communication
Sends alerts and receives responses
"""
```

- Class docstrings:
```python
class ZMQClient:
    """
    ZeroMQ Client for backend communication using REQ/REP pattern.

    Alert Format (matches backend python-client-with-notifications.py):
    { ... }
    """
```

- Function docstrings with Args/Returns:
```python
def send_url_alert(self, device_uid: str, url: str, ...) -> Optional[Dict[str, Any]]:
    """
    Send a UrlAlert.

    Args:
        device_uid: Unique device identifier
        url: URL to analyze

    Returns:
        Response dict with analysis results
    """
```

### Function Design

**Size:** 20-50 lines typical, some complex functions 100+ lines

**Parameters:**
- Explicit parameters with type hints and defaults: `def __init__(self, host: str = "localhost", port: int = 50001):`
- Keyword arguments for optional params

**Return Values:**
- `Optional[Dict]` for operations that may fail
- `bool` for success/failure
- Type hints on all public methods

### Module Design

**Exports:**
- Classes and functions exported implicitly
- Main guard pattern: `if __name__ == "__main__":`

**Architecture:**
- Dependency injection via `Container` class
- Service layer pattern: `ScanService`, `ProtectionService`, `MonitorService`
- Handler pattern: `ExtensionHandler`, `NotificationHandler`

---

## JavaScript Chrome Extension (apps/extension/chrome)

### Naming Patterns

**Files:**
- camelCase: `background.js`, `content.js`, `popup.js`
- PascalCase for modules: `StateManager.js`, `MessageBus.js`, `ProtectionService.js`

**Classes:**
- PascalCase: `ProtectionService`, `ConnectionService`, `StateManager`

**Functions:**
- camelCase: `executeAction()`, `handleUrlResult()`, `startLoadingState()`
- Async functions: `async function updateConnectionBadge(status) {}`

**Variables:**
- camelCase: `tabId`, `riskType`, `connectionStatus`
- Constants in UPPER_SNAKE_CASE: `BADGE_CONFIG`, `CONNECTION_STATUS`, `MSG`
- Object constants PascalCase: `const TrackerService = { ... }`

### Code Style

**Formatting:**
- No .eslintrc/.prettierrc detected
- 2-space indentation
- Single quotes preferred: `'connected'`, `'AntiScam Alert'`
- Template literals for complex strings: `` `Risk: ${riskLabels}` ``

**Linting:**
- No linter configuration detected

### Import Organization

**Order:**
- ES6 imports at top:
```javascript
import { stateManager } from './state/StateManager.js';
import { messageBus, MSG } from './messaging/index.js';
import { connectionService } from './services/ConnectionService.js';
```

**Path Aliases:**
- Relative imports: `'./services/...'`, `'../messaging/...'`

### Error Handling

**Patterns:**
- Try-catch with console logging:
```javascript
try {
  await chrome.tabs.sendMessage(tabId, { type: MSG.SHOW_WARNING });
} catch (e) {
  console.error('[ProtectionService] Error showing warning:', e);
}
```

- Optional chaining for safe property access: `data?.email`, `tabs[0]?.id`

### Logging

**Framework:** `console.log` / `console.error`

**Patterns:**
```javascript
console.log('[Background] Alarm triggered:', alarm.name);
console.error('[Background] Error from desktop:', data.message);
console.log(`[ProtectionService] Executing action: ${action}`);
```

**Prefixes:**
- `[Background]` for background service worker
- `[ProtectionService]` for service modules
- Component-specific tags in square brackets

### Comments

**When to Comment:**
- Section headers with visual separators:
```javascript
// ============================================
// AntiScam Extension - Background Service Worker
// Refactored with modular architecture
// ============================================
```

- Important behavioral notes:
```javascript
// Only show warning for incoming connections (dangerous)
// Skip non-injectable URLs
```

**JSDoc/TSDoc:** Not consistently used

### Function Design

**Size:** 10-50 lines typical

**Parameters:**
- Modern parameter patterns with destructuring when applicable
- Async/await for promises

**Return Values:**
- Promises for async operations
- No explicit return type declarations (vanilla JS, not TypeScript)

### Module Design

**Exports:**
- ES6 named exports: `export { stateManager }`
- Singleton pattern for services: `export const connectionService = new ConnectionService()`

**Barrel Files:**
- Used in `./messaging/index.js`, `./services/index.js` for re-exports

---

## Python URL Analyzer (basic-url-analyzer)

### Naming Patterns

**Files:**
- snake_case: `api.py`, `rules_engine.py`, `content_extractor.py`
- Test files: `test_known_sites.py`, `test_adversarial.py`

**Classes:**
- PascalCase: `ScamAnalyzer`, `RulesEngine`, `AnalyzeRequest`, `AnalyzeResponse`
- Pydantic models: `BaseModel` subclasses for API contracts

**Functions:**
- snake_case: `analyze_url()`, `setup_logger()`, `_analyze_content_patterns()`
- Private methods: `_determine_risk_level()`, `_analyze_whois_patterns()`
- Async FastAPI endpoints: `async def analyze_url(request: AnalyzeRequest):`

**Variables:**
- snake_case: `risk_score`, `detected_patterns`, `total_score`
- Constants: `FIXTURES_DIR = Path(__file__).parent / "fixtures"`

### Code Style

**Formatting:**
- Configured in `pyproject.toml`
- 4-space indentation
- Double quotes for strings
- F-strings for formatting

**Linting:**
- No explicit linter config, pytest configured in pyproject.toml

### Import Organization

**Order:**
1. Standard library
2. Third-party (FastAPI, Pydantic, pytest)
3. Local modules (`from core.analyzer import ...`)

**Example:**
```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, HttpUrl
from typing import Optional
import uvicorn

from core.analyzer import ScamAnalyzer
```

### Error Handling

**Patterns:**
- Try-except with structured error returns:
```python
try:
    result = analyzer.analyze_url(request.url)
    return AnalyzeResponse(...)
except Exception as e:
    raise HTTPException(status_code=500, detail=str(e))
```

- Graceful degradation with error fields:
```python
return {
    'success': False,
    'risk_score': 0,
    'error': f"Analysis failed: {str(e)}"
}
```

### Logging

**Framework:** Python `logging` with custom setup

**Setup:**
```python
from utils.logger import setup_logger

self.logger = setup_logger('rules_engine')
self.logger.info("Running rules engine analysis")
self.logger.error(f"Analysis failed: {str(e)}")
```

### Comments

**When to Comment:**
- Module docstrings:
```python
"""
FastAPI server for URL Scam Analyzer
Run with: uvicorn api:app --host 0.0.0.0 --port 8000
"""
```

- Test docstrings with requirements:
```python
"""
ZERO TOLERANCE: Known crypto exchange must classify as safe.

Tests: Coinbase, Binance, Kraken, Gemini, Crypto.com, Bitstamp, Bitso, CoinDCX
"""
```

### Function Design

**Size:** 20-100 lines, test functions 10-30 lines

**Parameters:**
- Type-hinted with Pydantic models for API
- Default parameters: `def __init__(self, use_cache=True, use_ml=True):`

**Return Values:**
- Structured dicts with consistent schema:
```python
{
    'success': True,
    'risk_score': 75,
    'detected_patterns': [...],
    'error': ''
}
```

---

## Cross-Project Patterns

### Shared Conventions

1. **Error returns over exceptions** for expected failures
2. **Verbose logging** with contextual prefixes
3. **Type safety** where language supports (C# nullability, Python type hints)
4. **Dependency injection** for testability (C# DI container, Python Container class)
5. **Structured results** with Success/Message/Data pattern

### Key Differences

| Aspect | C# Backend | Python Desktop | JS Extension | Python Analyzer |
|--------|-----------|----------------|--------------|-----------------|
| Naming | PascalCase | snake_case | camelCase | snake_case |
| Async | Task<T> | async/await | async/await | async/await |
| DI | Microsoft.Extensions.DI | Custom Container | Singleton services | FastAPI DI |
| Testing | None detected | Diagnostic scripts | None detected | pytest with fixtures |
| Logging | Console.WriteLine | logging + print | console.log | logging module |

---

*Convention analysis: 2026-02-13*
