using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Business.Views;
using Business.Data.EF;
using Business.Data.EF.Repositories;
using Interface.Repositories;
using Common.Entities;
using Common.Models;
using Microsoft.Extensions.Configuration;

namespace ASPS.Tests.Business.Views;

public class ASViewTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<ASView>> _loggerMock;
    private readonly ASView _asView;
    private readonly Mock<IConfiguration> _mockConfiguration;
    public ASViewTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ASView>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup service provider with repositories
        var services = new ServiceCollection();
        services.AddScoped(_ => _context);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IDeviceAlertRepository, DeviceAlertRepository>();
        services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();
        services.AddScoped<IKnownPhishingWebsiteRepository, KnownPhishingWebsiteRepository>();
        services.AddScoped<ISafeDomainRepository, SafeDomainRepository>();
        services.AddScoped<IWebsiteCategoryRepository, WebsiteCategoryRepository>(); // SCRUM-820
        
        // Add loggers for repositories
        services.AddSingleton(Mock.Of<ILogger<UserRepository>>());
        services.AddSingleton(Mock.Of<ILogger<UserDeviceRepository>>());
        services.AddSingleton(Mock.Of<ILogger<UserAccountRepository>>());
        services.AddSingleton(Mock.Of<ILogger<DeviceAlertRepository>>());
        services.AddSingleton(Mock.Of<ILogger<AnalysisResultRepository>>());
        services.AddSingleton(Mock.Of<ILogger<KnownPhishingWebsiteRepository>>());
        services.AddSingleton(Mock.Of<ILogger<SafeDomainRepository>>());
        services.AddSingleton(Mock.Of<ILogger<WebsiteCategoryRepository>>()); // SCRUM-820

        _serviceProvider = services.BuildServiceProvider();
        _asView = new ASView(_serviceProvider, _loggerMock.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act & Assert
        _asView.Should().NotBeNull();
    }

    #endregion

    #region Start/Stop Tests

    [Fact]
    public void Start_LoadsDataFromDatabase()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        // Act
        _asView.Start();

        // Assert
        var users = _asView.GetUsers();
        users.Should().HaveCount(1);
        users.First().Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Stop_DoesNotThrow()
    {
        // Act
        Action act = () => _asView.Stop();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region GetHandleableEvents Tests

    [Fact]
    public void GetHandleableEvents_ReturnsExpectedTypes()
    {
        // Act
        var result = _asView.GetHandleableEvents();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region FindUserByKey Tests

    [Fact]
    public void FindUserByKey_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.FindUserByKey(user.Key);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void FindUserByKey_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        _asView.Start();
        var nonExistentKey = new Key("User", "999");

        // Act
        var result = _asView.FindUserByKey(nonExistentKey);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindUserByEmail Tests

    [Fact]
    public void FindUserByEmail_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "find@example.com"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.FindUserByEmail("find@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("find@example.com");
    }

    [Fact]
    public void FindUserByEmail_CaseInsensitive()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "case@example.com"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.FindUserByEmail("CASE@EXAMPLE.COM");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("case@example.com");
    }

    [Fact]
    public void FindUserByEmail_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.FindUserByEmail("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindUserByEmailActive Tests

    [Fact]
    public void FindUserByEmailActive_WhenUserIsActive_ReturnsUser()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Active",
            LastName = "User",
            Email = "active@example.com",
            IsDisabled = false
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.FindUserByEmailActive("active@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("active@example.com");
    }

    [Fact]
    public void FindUserByEmailActive_WhenUserIsDisabled_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Disabled",
            LastName = "User",
            Email = "disabled@example.com",
            IsDisabled = true
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.FindUserByEmailActive("disabled@example.com");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUsers Tests

    [Fact]
    public void GetUsers_ReturnsAllUsers()
    {
        // Arrange
        var user1 = new User { KeyField = Guid.NewGuid().ToString(), FirstName = "User", LastName = "One", Email = "user1@example.com" };
        var user2 = new User { KeyField = Guid.NewGuid().ToString(), FirstName = "User", LastName = "Two", Email = "user2@example.com" };
        _context.Users.AddRange(user1, user2);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetUsers();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.Email == "user1@example.com");
        result.Should().Contain(u => u.Email == "user2@example.com");
    }

    [Fact]
    public void GetUsers_WhenNoUsers_ReturnsEmptyList()
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.GetUsers();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region IsSafeDomain Tests

    [Fact]
    public void IsSafeDomain_WhenDomainIsSafe_ReturnsTrue()
    {
        // Arrange
        var safeDomain = new SafeDomain
        {
            Domain = "safe.com",
            DateCreated = DateTime.UtcNow
        };
        _context.SafeDomains.Add(safeDomain);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.IsSafeDomain("safe.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSafeDomain_CaseInsensitive()
    {
        // Arrange
        var safeDomain = new SafeDomain
        {
            Domain = "safe.com",
            DateCreated = DateTime.UtcNow
        };
        _context.SafeDomains.Add(safeDomain);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.IsSafeDomain("SAFE.COM");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSafeDomain_WhenDomainIsNotSafe_ReturnsFalse()
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.IsSafeDomain("unsafe.com");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafeDomain_WhenDomainIsNullOrWhitespace_ReturnsFalse(string? domain)
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.IsSafeDomain(domain!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetKnownPhishingWebsites Tests

    [Fact]
    public void GetKnownPhishingWebsites_ReturnsAllPhishingWebsites()
    {
        // Arrange
        var phishing1 = new KnownPhishingWebsite("http://phishing1.com");
        var phishing2 = new KnownPhishingWebsite("http://phishing2.com");
        _context.KnownPhishingWebsites.AddRange(phishing1, phishing2);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetKnownPhishingWebsites();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetSafeDomains Tests

    [Fact]
    public void GetSafeDomains_ReturnsAllSafeDomains()
    {
        // Arrange
        var safe1 = new SafeDomain { Domain = "safe1.com", DateCreated = DateTime.UtcNow };
        var safe2 = new SafeDomain { Domain = "safe2.com", DateCreated = DateTime.UtcNow };
        _context.SafeDomains.AddRange(safe1, safe2);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetSafeDomains();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region Initialize and ReInitialize Tests

    [Fact]
    public void Initialize_OnFirstCall_LoadsData()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PhoneNumber = "1234567890"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        // Act
        _asView.Start();

        // Assert
        var users = _asView.GetUsers();
        users.Should().HaveCount(1);
        users[0].Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Start_CalledTwice_DoesNotThrowException()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PhoneNumber = "1234567890"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        // Act
        _asView.Start();
        
        // Second call to Start() - should not throw (IsInitialized check)
        Action secondStart = () => _asView.Start();

        // Assert
        secondStart.Should().NotThrow();
    }

    [Fact]
    public void ReInitialize_CanBeCalledSuccessfully()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PhoneNumber = "1234567890"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        _asView.Start();

        // Act - ReInitialize() should reset IsInitialized and reload
        Action reInit = () => _asView.ReInitializeAsync();

        // Assert - Should not throw
        reInit.Should().NotThrow();
        
        // Data should still be accessible
        var users = _asView.GetUsers();
        users.Should().HaveCount(1);
    }

    #endregion

    #region WebsiteCategoryViews Tests (SCRUM-820)

    [Fact]
    public void WebsiteCategoryViews_OnInitialize_LoadsCategoriesFromRepository()
    {
        // Arrange
        var category1 = new WebsiteCategory("Social Media", null, "Test");
        var category2 = new WebsiteCategory("News", null, "Test");
        _context.WebsiteCategories.AddRange(category1, category2);
        _context.SaveChanges();

        // Act
        _asView.Start();

        // Assert
        _asView.WebsiteCategoryViews.Should().HaveCount(2);
        _asView.WebsiteCategoryViews.Should().Contain(c => c.Tag.Name == "Social Media");
        _asView.WebsiteCategoryViews.Should().Contain(c => c.Tag.Name == "News");
    }

    [Fact]
    public void GetCategoryView_WhenCategoryExists_ReturnsCategory()
    {
        // Arrange
        var category = new WebsiteCategory("Entertainment", null, "Test");
        _context.WebsiteCategories.Add(category);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetCategoryView("Entertainment");

        // Assert
        result.Should().NotBeNull();
        result!.Tag.Name.Should().Be("Entertainment");
    }

    [Fact]
    public void GetCategoryView_CaseInsensitive()
    {
        // Arrange
        var category = new WebsiteCategory("Shopping", null, "Test");
        _context.WebsiteCategories.Add(category);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetCategoryView("SHOPPING");

        // Assert
        result.Should().NotBeNull();
        result!.Tag.Name.Should().Be("Shopping");
    }

    [Fact]
    public void GetCategoryView_WhenCategoryDoesNotExist_ReturnsNull()
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.GetCategoryView("NonExistentCategory");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCategoryView_WhenCategoryNameIsNullOrWhitespace_ReturnsNull(string? categoryName)
    {
        // Arrange
        _asView.Start();

        // Act
        var result = _asView.GetCategoryView(categoryName!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCategoryView_WithParentCategory_ReturnsCorrectHierarchy()
    {
        // Arrange
        var parentCategory = new WebsiteCategory("Tech", null, "Test");
        _context.WebsiteCategories.Add(parentCategory);
        _context.SaveChanges();

        var childCategory = new WebsiteCategory("Programming", parentCategory.KeyField, "Test");
        _context.WebsiteCategories.Add(childCategory);
        _context.SaveChanges();

        _asView.Start();

        // Act
        var result = _asView.GetCategoryView("Programming");

        // Assert
        result.Should().NotBeNull();
        result!.Tag.Name.Should().Be("Programming");
        result.Parent.Should().NotBeNull();
        result.Parent!.Tag.Name.Should().Be("Tech");
    }

    #endregion
}
