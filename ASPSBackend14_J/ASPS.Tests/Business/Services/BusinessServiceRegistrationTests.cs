using Business.Data.EF;
using Business.Data.EF.Repositories;
using Business.Handlers;
using Business.Services;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASPS.Tests.Business.Services;

public class BusinessServiceRegistrationTests
{
    [Fact]
    public void AddBusinessServices_WithValidConnectionString_ShouldRegisterDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - check service descriptor was added
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(AppDbContext));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddBusinessServices_WithValidConnectionString_ShouldRegisterRepositories()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - check all repositories are registered
        Assert.Contains(services, s => s.ServiceType == typeof(IUserRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IUserDeviceRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IUserAccountRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IDeviceAlertRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IAnalysisResultRepository));
        Assert.Contains(services, s => s.ServiceType == typeof(IKnownPhishingWebsiteRepository));
    }

    [Fact]
    public void AddBusinessServices_WithValidConnectionString_ShouldRegisterHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - check all handlers are registered
        Assert.Contains(services, s => s.ServiceType == typeof(UserCommandHandlers));
        Assert.Contains(services, s => s.ServiceType == typeof(UserQueryHandlers));
        Assert.Contains(services, s => s.ServiceType == typeof(AdminCommandHandlers));
        Assert.Contains(services, s => s.ServiceType == typeof(AdminQueryHandlers));
    }

    [Fact]
    public void AddBusinessServices_WithNullConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddBusinessServices(null!));
        
        Assert.Contains("connection string is required", exception.Message);
    }

    [Fact]
    public void AddBusinessServices_WithEmptyConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddBusinessServices(string.Empty));
        
        Assert.Contains("connection string is required", exception.Message);
    }

    [Fact]
    public void AddBusinessServices_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        var result = services.AddBusinessServices(connectionString);

        // Assert - should return the same collection for fluent API
        Assert.Same(services, result);
    }

    [Fact]
    public void AddBusinessServices_RepositoryImplementations_ShouldBeCorrectTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - verify concrete implementations from descriptors
        var userRepo = services.FirstOrDefault(s => s.ServiceType == typeof(IUserRepository));
        Assert.NotNull(userRepo);
        Assert.Equal(typeof(UserRepository), userRepo!.ImplementationType);

        var deviceRepo = services.FirstOrDefault(s => s.ServiceType == typeof(IUserDeviceRepository));
        Assert.NotNull(deviceRepo);
        Assert.Equal(typeof(UserDeviceRepository), deviceRepo!.ImplementationType);
    }

    [Fact]
    public void AddBusinessServices_ServicesLifetime_ShouldBeScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=testdb;User=test;Password=test;";

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - verify services are registered as Scoped
        var repositoryDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IUserRepository));
        Assert.NotNull(repositoryDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, repositoryDescriptor!.Lifetime);

        var handlerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(UserCommandHandlers));
        Assert.NotNull(handlerDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, handlerDescriptor!.Lifetime);
    }

    [Theory]
    [InlineData("Server=localhost;Database=mydb;User=root;Password=secret;")]
    [InlineData("Server=192.168.1.1;Database=proddb;User=admin;Password=pass;")]
    [InlineData("Server=sql.example.com;Database=testdb;User=test;Password=test123;")]
    public void AddBusinessServices_WithDifferentConnectionStrings_ShouldWork(string connectionString)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBusinessServices(connectionString);

        // Assert - verify DbContext service descriptor exists
        var dbContextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(AppDbContext));
        Assert.NotNull(dbContextDescriptor);
    }
}
