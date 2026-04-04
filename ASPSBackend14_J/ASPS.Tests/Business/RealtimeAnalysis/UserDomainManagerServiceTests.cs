using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Business.Data.EF;
using Business.RealtimeAnalysis.UserDomain;
using Business.Views;
using Common.Entities;
using Common.Models;
using Common.Interfaces;
using Interface.Repositories;
using Business.DomainEvents;

namespace ASPS.Tests.Business.RealtimeAnalysis;

public class UserDomainManagerServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<UserDomainManagerService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ASView> _asViewMock;
    private readonly Mock<IKnownPhishingWebsiteRepository> _phishingRepoMock;
    private readonly Mock<ISafeDomainRepository> _safeDomainRepoMock;
    private readonly UserDomainManagerService _sut;

    public UserDomainManagerServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<UserDomainManagerService>>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        
        // Create real configuration for tests
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Python:ExecutablePath"] = "python",
            ["Python:AnalyzersFolderPath"] = "/tmp/analyzers"
        });
        _configuration = configBuilder.Build();
        
        // ASView requires IServiceProvider and ILogger<ASView>
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockAsViewLogger = new Mock<ILogger<ASView>>();
        _asViewMock = new Mock<ASView>(mockServiceProvider.Object, mockAsViewLogger.Object);
        
        _phishingRepoMock = new Mock<IKnownPhishingWebsiteRepository>();
        _safeDomainRepoMock = new Mock<ISafeDomainRepository>();

        var eventHandlers = new List<IDomainEventHandler>();

        _sut = new UserDomainManagerService(
            _loggerFactoryMock.Object,
            _configuration,
            _context,
            _asViewMock.Object,
            eventHandlers,
            _phishingRepoMock.Object,
            _safeDomainRepoMock.Object);
    }

    #region GetOrCreateManagerForUser Tests

    [Fact]
    public void GetOrCreateManagerForUser_WhenUserExists_CreatesManager()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "John",
            LastName = "Doe",
            IsDeleted = false
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        var userKey = new Key("User", keyField, "default");

        // Act
        var result = _sut.GetOrCreateManagerForUser(userKey);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreateManagerForUser_WhenUserNotFound_ThrowsException()
    {
        // Arrange
        var userKey = new Key("User", "non-existent", "default");

        // Act
        Action act = () => _sut.GetOrCreateManagerForUser(userKey);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*User not found*");
    }

    [Fact]
    public void GetOrCreateManagerForUser_CalledTwice_ReturnsSameManager()
    {
        // Arrange
        var keyField = Guid.NewGuid().ToString();
        var user = new User
        {
            KeyField = keyField,
            FirstName = "Jane",
            LastName = "Smith",
            IsDeleted = false
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        var userKey = new Key("User", keyField, "default");

        // Act
        var manager1 = _sut.GetOrCreateManagerForUser(userKey);
        var manager2 = _sut.GetOrCreateManagerForUser(userKey);

        // Assert
        manager1.Should().BeSameAs(manager2);
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
