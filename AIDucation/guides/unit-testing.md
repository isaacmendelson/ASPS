---
title: "Unit Testing Guide - ASPS"
version: 1.0.0
last_updated: 2026-03-12
author: QA Team
---

# מדריך כתיבת Unit Tests ל-ASPS 🧪

## תוכן עניינים
1. [סביבת העבודה](#סביבת-העבודה)
2. [ספריות נדרשות](#ספריות-נדרשות)
3. [מבנה קובץ טסט](#מבנה-קובץ-טסט)
4. [דוגמאות קוד](#דוגמאות-קוד)
5. [Best Practices](#best-practices)
6. [הרצת טסטים](#הרצת-טסטים)

---

## סביבת העבודה

### מיקום הטסטים
```
ASPSBackend14_J/
└── ASPS.Tests/
    ├── Common/          # טסטים ל-Entities, Models
    ├── Business/        # טסטים ל-Services, Handlers, Repositories
    ├── Interface/       # טסטים ל-Interfaces
    └── WebApi/          # טסטים ל-Controllers
```

### כלל: קובץ טסט לכל Class
```
UserRepository.cs → UserRepositoryTests.cs
IndicatorFactory.cs → IndicatorFactoryTests.cs
```

---

## ספריות נדרשות

```csharp
using Xunit;                    // Testing framework
using Moq;                      // Mocking
using FluentAssertions;         // Assertions
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;  // InMemory DB
```

### NuGet Packages (כבר מותקנים)
- `xunit` - Framework לטסטים
- `Moq` - יצירת Mocks
- `FluentAssertions` - Assertions קריאים
- `Microsoft.EntityFrameworkCore.InMemory` - DB בזיכרון

---

## מבנה קובץ טסט

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ASPS.Tests.Business
{
    public class MyClassTests
    {
        // Dependencies
        private readonly Mock<ILogger<MyClass>> _loggerMock;
        private readonly Mock<IRepository> _repoMock;
        
        // System Under Test
        private readonly MyClass _sut;

        public MyClassTests()
        {
            // Setup mocks
            _loggerMock = new Mock<ILogger<MyClass>>();
            _repoMock = new Mock<IRepository>();
            
            // Create instance
            _sut = new MyClass(_loggerMock.Object, _repoMock.Object);
        }

        #region MethodName Tests

        [Fact]
        public void MethodName_WhenCondition_ShouldExpectedResult()
        {
            // Arrange
            var input = "test";
            _repoMock.Setup(r => r.Get(It.IsAny<int>())).Returns(new Entity());

            // Act
            var result = _sut.MethodName(input);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("expected");
        }

        [Theory]
        [InlineData("input1", "expected1")]
        [InlineData("input2", "expected2")]
        public void MethodName_WithDifferentInputs_ReturnsExpected(string input, string expected)
        {
            // Arrange & Act
            var result = _sut.MethodName(input);

            // Assert
            result.Should().Be(expected);
        }

        #endregion
    }
}
```

---

## דוגמאות קוד

### 1. טסט פשוט עם Fact

```csharp
[Fact]
public void Constructor_WithValidParams_CreatesInstance()
{
    // Arrange
    var name = "Test";
    var value = 42;

    // Act
    var result = new MyClass(name, value);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("Test");
    result.Value.Should().Be(42);
}
```

### 2. טסט עם Theory (מספר מקרים)

```csharp
[Theory]
[InlineData(null, false)]
[InlineData("", false)]
[InlineData("valid", true)]
public void IsValid_WithDifferentInputs_ReturnsExpected(string input, bool expected)
{
    // Act
    var result = MyClass.IsValid(input);

    // Assert
    result.Should().Be(expected);
}
```

### 3. טסט עם Mock

```csharp
[Fact]
public void GetUser_WhenUserExists_ReturnsUser()
{
    // Arrange
    var userId = 1;
    var expectedUser = new User { Id = 1, Name = "John" };
    
    _userRepoMock
        .Setup(r => r.GetByIdAsync(userId))
        .ReturnsAsync(expectedUser);

    // Act
    var result = await _sut.GetUser(userId);

    // Assert
    result.Should().NotBeNull();
    result.Name.Should().Be("John");
    _userRepoMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
}
```

### 4. טסט עם InMemory Database

```csharp
public class RepositoryTests
{
    private readonly AppDbContext _context;
    private readonly MyRepository _repository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new MyRepository(_context, Mock.Of<ILogger<MyRepository>>());
    }

    [Fact]
    public async Task AddAsync_WithValidEntity_AddsToDatabase()
    {
        // Arrange
        var entity = new MyEntity { Name = "Test" };

        // Act
        await _repository.AddAsync(entity);

        // Assert
        _context.MyEntities.Should().HaveCount(1);
        _context.MyEntities.First().Name.Should().Be("Test");
    }
}
```

### 5. טסט לחריגות (Exceptions)

```csharp
[Fact]
public void Method_WithNullInput_ThrowsArgumentNullException()
{
    // Arrange
    string input = null;

    // Act
    Action act = () => _sut.Method(input);

    // Assert
    act.Should().Throw<ArgumentNullException>()
       .WithParameterName("input");
}
```

### 6. טסט לאירועים (Events)

```csharp
[Fact]
public void Start_WhenCalled_RaisesStartedEvent()
{
    // Arrange
    var eventRaised = false;
    _sut.Started += (s, e) => eventRaised = true;

    // Act
    _sut.Start();

    // Assert
    eventRaised.Should().BeTrue();
}
```

---

## Best Practices

### ✅ כן לעשות

1. **שם ברור לטסט:** `MethodName_WhenCondition_ShouldResult`
2. **AAA Pattern:** Arrange, Act, Assert
3. **טסט אחד = בדיקה אחת**
4. **Mock לכל dependency חיצוני**
5. **FluentAssertions לקריאות**
6. **Regions לארגון:**
   ```csharp
   #region Constructor Tests
   #region MethodName Tests
   ```

### ❌ לא לעשות

1. **לא** לבדוק קוד של ספריות חיצוניות
2. **לא** לבדוק יותר מדבר אחד בטסט
3. **לא** להשתמש ב-DB אמיתי
4. **לא** להשאיר טסטים מושבתים
5. **לא** לכתוב טסטים שתלויים אחד בשני

---

## הרצת טסטים

### הרצת כל הטסטים
```bash
cd ASPSBackend14_J
dotnet test
```

### הרצת קובץ ספציפי
```bash
dotnet test --filter "FullyQualifiedName~MyClassTests"
```

### הרצה עם פירוט
```bash
dotnet test --verbosity detailed
```

### בדיקה לפני commit
```bash
dotnet build && dotnet test
```
**חובה: Build + Tests חייבים לעבור לפני כל commit!**

---

## Checklist לפני סיום משימה

- [ ] כל המתודות הציבוריות מכוסות
- [ ] Edge cases נבדקים (null, empty, boundaries)
- [ ] `dotnet test` עובר (כל הטסטים!)
- [ ] קוד נקי וקריא
- [ ] JIRA מעודכן

---

## שאלות?

אם משהו לא ברור:
1. שאל את Alex (CTO)
2. אם הוא לא יודע → Zappa (CEO)
3. אם גם הוא לא יודע → Isaac

**אסור להניח. חייב לשאול!** 🚨
