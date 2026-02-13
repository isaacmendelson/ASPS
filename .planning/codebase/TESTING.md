# Testing Patterns

**Analysis Date:** 2026-02-13

## Test Framework Status by Project

| Project | Test Framework | Status | Coverage |
|---------|---------------|--------|----------|
| ASPSBackend14_J (C#) | None | No automated tests | 0% |
| apps/desktop/win (Python) | Manual diagnostic scripts | Ad-hoc testing only | Unknown |
| apps/extension/chrome (JS) | None | No automated tests | 0% |
| basic-url-analyzer (Python) | pytest | Comprehensive test suite | Unknown |

---

## C# Backend (ASPSBackend14_J)

### Test Framework

**Status:** No automated testing framework detected

**Test-like files found:**
- `C:\Users\pc\Desktop\asps\ASPSBackend14_J\DbContextTest.cs` - Database connection diagnostic
- `C:\Users\pc\Desktop\asps\ASPSBackend14_J\TestDatabaseConnection.cs` - Manual database test
- `C:\Users\pc\Desktop\asps\ASPSBackend14_J\TEST-RUNTIME.cs` - Runtime diagnostic

These are **diagnostic utilities**, not unit tests.

### Diagnostic Pattern

**DbContextTest.cs example:**
```csharp
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("DbContext Initialization Test");
        Console.WriteLine("========================================");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Manual service setup
        var services = new ServiceCollection();
        services.AddLogging(builder => {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        try {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Console.WriteLine("[TEST] ✓ DbContext created successfully!");

            var canConnect = context.Database.CanConnect();
            Console.WriteLine($"[TEST] ✓ Database connection: {canConnect}");

            Console.WriteLine("\n[TEST] ✓✓✓ ALL TESTS PASSED ✓✓✓");
        } catch (Exception ex) {
            Console.WriteLine("\n[TEST] ✗✗✗ FAILURE ✗✗✗");
            Console.WriteLine($"Message: {ex.Message}");
        }
    }
}
```

**Characteristics:**
- Console application with Main entry point
- Manual dependency injection setup
- Visual console output with success/failure indicators
- No assertions - relies on exceptions or manual verification
- Run manually, not part of build pipeline

### Testing Recommendations

**What should be tested:**
- Command handlers (CreateUserCommand, UpdateUserCommand)
- Query handlers (GetAllUsersQuery, GetUserByKeyQuery)
- Repository operations (CRUD operations)
- Domain event handling
- CQRS message processing

**Suggested framework:**
- xUnit or NUnit for unit tests
- Moq for mocking repositories/dependencies
- FluentAssertions for readable assertions

---

## Python Desktop App (apps/desktop/win)

### Test Framework

**Status:** No pytest or unittest detected

**Test-like files found:**
- `C:\Users\pc\Desktop\asps\apps\desktop\win\test.py` - Unknown content
- `C:\Users\pc\Desktop\asps\apps\desktop\win\src\diag_ws_test.py` - WebSocket diagnostic
- `C:\Users\pc\Desktop\asps\apps\desktop\win\src\diag_zmq_test.py` - ZMQ diagnostic

### Diagnostic Scripts

**Purpose:** Verify connectivity to backend services

**Pattern:**
- Standalone scripts with `if __name__ == "__main__":`
- Interactive prompts for test parameters
- Print-based output verification
- Manual execution

**Example pattern (from zmq_client.py):**
```python
if __name__ == "__main__":
    import sys
    logging.basicConfig(level=logging.INFO)

    print("=" * 70)
    print("ZMQ CLIENT - STANDALONE TEST")
    print("=" * 70)

    host = sys.argv[1] if len(sys.argv) > 1 else "localhost"
    client = ZMQClient(host, 50001)

    print("\nMENU: Choose test:")
    print("1. URL Alert")
    print("2. Remote Access Alert")

    choice = input("\nEnter choice (1-2, default=1): ").strip() or "1"

    if choice == "1":
        url = input("Enter URL (default=http://example.com): ").strip() or "http://example.com"
        response = client.send_url_alert(device_uid="PC-TEST-001", url=url)

        if response:
            print(f"\nSUCCESS: Test completed!")
        else:
            print("\nERROR: Test failed!")
```

**Characteristics:**
- Interactive terminal interface
- Hardcoded test data (device_uid="PC-TEST-001")
- Success/failure determined by return value existence
- No automated assertions
- Manual verification of output

### Testing Recommendations

**What should be tested:**
- ZMQ message serialization/deserialization
- Cache manager operations (get/set/expiry)
- Auth token refresh logic
- WebSocket message handling
- Service layer logic (ScanService, ProtectionService)

**Suggested framework:**
- pytest with pytest-asyncio for async tests
- unittest.mock for mocking external dependencies
- pytest-mock for cleaner mock syntax

---

## JavaScript Chrome Extension (apps/extension/chrome)

### Test Framework

**Status:** No test framework detected

**No test files found** - no `.test.js`, `.spec.js`, or test directory

**No test runners:** No Jest, Mocha, or Jasmine configuration

### Manual Testing Approach

Based on code structure, testing appears to be:

1. **Browser-based manual testing**
   - Load extension in Chrome
   - Navigate to test URLs
   - Verify popup behavior
   - Check console logs

2. **Console debugging**
   - Extensive `console.log` statements throughout code:
   ```javascript
   console.log('[Background] Alarm triggered:', alarm.name);
   console.error('[ProtectionService] Error showing warning:', e);
   console.log(`[ConnectionService] Connected to desktop app`);
   ```

3. **Visual verification**
   - Warning banners, modals, and page blocking
   - Badge color changes (green/red/yellow)
   - Icon state changes

### Testing Recommendations

**What should be tested:**
- Message passing between background/content scripts
- Cache service operations
- State management
- Protection service action execution
- Connection service WebSocket handling

**Suggested framework:**
- Jest with chrome extension mocks
- @types/chrome for TypeScript support (if migrating)
- Sinon for stubbing Chrome APIs

---

## Python URL Analyzer (basic-url-analyzer)

### Test Framework

**Runner:** pytest

**Config:** `C:\Users\pc\Desktop\asps\basic-url-analyzer\basic-url-analyzer\basic-url-analyzer\pyproject.toml`

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
python_files = ["test_*.py"]
python_functions = ["test_*"]
addopts = "-v --tb=short"
markers = [
    "slow: marks tests as slow (deselect with '-m \"not slow\"')",
    "adversarial: adversarial attack tests",
    "known_sites: known legitimate site tests",
    "production_dist: production distribution tests",
]
```

**Run Commands:**
```bash
pytest                    # Run all tests
pytest -v                 # Verbose output
pytest -m "not slow"      # Skip slow tests
pytest -m known_sites     # Run only known site tests
pytest tests/test_known_sites.py  # Run specific test file
```

### Test File Organization

**Location:** Co-located test directory

**Naming:**
- Test files: `test_*.py` pattern
- Test functions: `test_*` pattern

**Structure:**
```
basic-url-analyzer/
├── core/
│   ├── rules_engine.py
│   └── ml_classifier.py
├── tests/
│   ├── conftest.py              # Shared fixtures
│   ├── test_known_sites.py      # Known legitimate site tests
│   ├── test_adversarial.py      # Attack/adversarial tests
│   └── fixtures/
│       └── known_sites.json     # Test data
└── scripts/
    └── run_tests.py             # Test runner script
```

### Test Structure

**Suite Organization:**

`test_known_sites.py` example:
```python
"""
TEST-02: Known legitimate site verification tests.

Zero-tolerance tests ensuring known legitimate sites (crypto exchanges, banks)
are never incorrectly flagged as scams.

Test Coverage:
- 8 major crypto exchanges
- 10 major financial institutions

ZERO FALSE POSITIVES ALLOWED - Any false positive fails the entire test suite.
"""

import json
from pathlib import Path
from typing import List, Dict
import pytest

# Fixtures
@pytest.fixture(scope="module")
def known_sites() -> List[Dict]:
    """Load known site fixtures."""
    fixtures_path = FIXTURES_DIR / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        return json.load(f)

@pytest.fixture(scope="module")
def crypto_sites(known_sites) -> List[Dict]:
    """Filter for crypto exchange sites only."""
    return [s for s in known_sites if s["category"] == "crypto_exchange"]

# Test class
class TestKnownCryptoExchanges:
    """Test that known crypto exchanges are classified as safe."""

    @pytest.mark.known_sites
    @pytest.mark.parametrize("site_name,text", get_crypto_sites())
    def test_known_crypto_site_classifies_safe(self, classifier, site_name, text):
        """
        ZERO TOLERANCE: Known crypto exchange must classify as safe.
        """
        result = classifier.predict(text)

        # Zero tolerance assertion
        assert not result["is_scam"], (
            f"FALSE POSITIVE: {site_name.upper()} incorrectly flagged as scam! "
            f"(scam confidence: {result['confidence']:.1%}). "
            f"This is a known legitimate crypto exchange."
        )

        # Confidence check
        assert result["confidence"] < 0.5, (
            f"WARNING: {site_name.upper()} has high scam confidence "
            f"({result['confidence']:.1%})."
        )
```

**Patterns:**
- Module-scoped fixtures for expensive setup
- Parametrized tests for data-driven testing
- Custom error messages with context
- Markers for test categorization
- Descriptive docstrings explaining test purpose

### Fixtures and Factories

**Fixture Location:**
- `tests/conftest.py` for shared fixtures
- `tests/fixtures/` for JSON test data

**Test Data Pattern:**
```python
FIXTURES_DIR = Path(__file__).parent / "fixtures"

@pytest.fixture(scope="module")
def known_sites() -> List[Dict]:
    """Load known site fixtures from JSON."""
    fixtures_path = FIXTURES_DIR / "known_sites.json"
    with open(fixtures_path, "r", encoding="utf-8") as f:
        return json.load(f)
```

**Fixture composition:**
```python
@pytest.fixture(scope="module")
def crypto_sites(known_sites) -> List[Dict]:
    """Derived fixture - filter crypto exchanges."""
    return [s for s in known_sites if s["category"] == "crypto_exchange"]
```

### Coverage

**Requirements:** Not enforced (no coverage configuration found)

**Current state:** Unknown - no coverage reports found

**View Coverage:**
```bash
pytest --cov=core --cov=scrapers --cov-report=html
```

### Test Types

**Unit Tests:** Not explicitly separated

**Integration Tests:**
- Test files interact with actual analyzer components
- Use real ML classifier and rules engine
- Load actual test data from fixtures

**E2E Tests:** Not detected

### Common Patterns

**Parametrized Testing:**
```python
@pytest.mark.parametrize("site_name,text", get_crypto_sites())
def test_known_crypto_site_classifies_safe(self, classifier, site_name, text):
    result = classifier.predict(text)
    assert not result["is_scam"]
```

**Custom Assertions with Context:**
```python
assert not result["is_scam"], (
    f"FALSE POSITIVE: {site_name.upper()} incorrectly flagged! "
    f"Confidence: {result['confidence']:.1%}"
)
```

**Fixture Scoping:**
```python
@pytest.fixture(scope="module")  # Reuse across test class
@pytest.fixture(scope="function")  # New instance per test
```

**Test Markers:**
```python
@pytest.mark.known_sites
@pytest.mark.slow
@pytest.mark.adversarial
```

**Data-Driven Tests:**
- JSON fixtures for test data
- Parametrized tests iterate over datasets
- Helper functions generate test parameters

---

## Cross-Project Testing Gaps

### Critical Missing Tests

**C# Backend:**
- Command/Query handler unit tests
- Repository integration tests with test database
- Domain event handling tests
- CQRS message serialization tests
- NetMQ communication tests

**Python Desktop:**
- ZMQ client unit tests (mock socket)
- Cache manager tests (expiry, persistence)
- Auth manager tests (token refresh)
- Service layer tests (protection, scan)
- Handler tests (extension, notification)

**Chrome Extension:**
- Message passing tests (background ↔ content)
- State management tests
- Cache service tests
- Connection service WebSocket tests
- Protection service action tests

### Testing Best Practices Observed

**URL Analyzer (only project with tests):**

✓ **Clear test purpose** - Docstrings explain what and why
✓ **Zero-tolerance approach** - Explicit failure messages
✓ **Fixture-based data** - Reusable, version-controlled test data
✓ **Parametrized tests** - DRY principle for similar tests
✓ **Test categorization** - Markers for selective execution
✓ **Readable assertions** - Custom messages with context

### Recommended Test Structure (for projects lacking tests)

**C# Backend:**
```
ASPSBackend14_J.Tests/
├── Handlers/
│   ├── UserCommandHandlersTests.cs
│   └── UserQueryHandlersTests.cs
├── Repositories/
│   └── RepositoryTests.cs
└── Integration/
    └── DatabaseIntegrationTests.cs
```

**Python Desktop:**
```
apps/desktop/win/
├── tests/
│   ├── conftest.py
│   ├── test_zmq_client.py
│   ├── test_cache_manager.py
│   ├── test_auth_manager.py
│   └── test_services.py
```

**Chrome Extension:**
```
apps/extension/chrome/
├── tests/
│   ├── setup.js
│   ├── background.test.js
│   ├── services/
│   │   ├── ConnectionService.test.js
│   │   └── ProtectionService.test.js
│   └── mocks/
│       └── chrome-api.js
```

---

*Testing analysis: 2026-02-13*
