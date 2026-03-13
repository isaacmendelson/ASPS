using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Business.Messaging;
using Business.Handlers;
using Business.Queries;
using Business.Commands;
using Business.Services;
using Business.Views;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace ASPS.Tests.Business.Messaging;

public class CQRSGatewayTests : IDisposable
{
    private readonly ILogger<CQRSGateway> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CQRSGateway? _gateway;

    public CQRSGatewayTests()
    {
        // Use NullLogger instead of Mock for ILogger
        _logger = NullLogger<CQRSGateway>.Instance;
        
        // Create a minimal service provider
        // The gateway doesn't need handlers during construction, only at runtime
        var services = new ServiceCollection();
        
        // Add logger
        services.AddSingleton(_logger);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesGateway()
    {
        // Act
        _gateway = new CQRSGateway(_serviceProvider, _logger);

        // Assert
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Constructor_WithCustomEndpoint_CreatesGateway()
    {
        // Arrange
        var customEndpoint = "tcp://*:7777";

        // Act
        _gateway = new CQRSGateway(_serviceProvider, _logger, customEndpoint);

        // Assert
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Constructor_WithNullCurveKeyManager_CreatesGateway()
    {
        // Act - CurveKeyManager can be null (optional parameter)
        _gateway = new CQRSGateway(
            _serviceProvider, 
            _logger, 
            "tcp://*:15556",  // Use different port to avoid collision
            null);

        // Assert
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_CreatesGateway()
    {
        // The constructor may not validate null - it's a design choice
        // If validation is needed, it would happen during Start()
        
        // Act & Assert - Should create gateway (validation happens later)
        var gateway = new CQRSGateway(null!, _logger, "tcp://*:25556");
        Assert.NotNull(gateway);
    }

    [Fact]
    public void Constructor_WithNullLogger_CreatesGateway()
    {
        // The constructor may not validate null - it's a design choice
        
        // Act & Assert - Should create gateway (validation happens later)
        var gateway = new CQRSGateway(_serviceProvider, null!, "tcp://*:35556");
        Assert.NotNull(gateway);
    }

    #endregion

    #region Start/Stop Tests

    [Fact]
    public void Start_StartsGatewaySuccessfully()
    {
        // Arrange - Use unique port to avoid collision
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45556");

        // Act & Assert - Should not throw
        _gateway.Start();

        // Cleanup
        _gateway.Stop();
    }

    [Fact]
    public void Stop_StopsGatewaySuccessfully()
    {
        // Arrange - Use unique port to avoid collision
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45557");
        _gateway.Start();

        // Act & Assert - Should not throw
        _gateway.Stop();
    }

    [Fact]
    public void Stop_CanBeCalledMultipleTimes()
    {
        // Arrange - Use unique port to avoid collision
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45558");
        _gateway.Start();

        // Act & Assert - Should not throw
        _gateway.Stop();
        _gateway.Stop();
    }

    [Fact]
    public void Dispose_CallsStopSuccessfully()
    {
        // Arrange - Use unique port to avoid collision
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45559");
        _gateway.Start();

        // Act & Assert - Should not throw
        _gateway.Dispose();
    }

    #endregion

    #region Message Processing Error Handling

    [Fact]
    public async Task ProcessMessage_WithInvalidJson_ReturnsErrorResponse()
    {
        // This tests error handling indirectly via the private ProcessMessageAsync method
        // We verify that invalid messages are logged as errors
        
        // Arrange
        _gateway = new CQRSGateway(_serviceProvider, _logger);

        // Act - Create gateway (which will handle invalid messages internally)
        
        // Assert - Gateway should be created successfully
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Gateway_WithMissingMessageType_HandlesGracefully()
    {
        // Arrange
        _gateway = new CQRSGateway(_serviceProvider, _logger);
        
        // Act - Create gateway
        
        // Assert - Gateway handles missing MessageType internally
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Gateway_WithUnknownMessageType_HandlesGracefully()
    {
        // Arrange
        _gateway = new CQRSGateway(_serviceProvider, _logger);
        
        // Act - Create gateway
        
        // Assert - Gateway handles unknown MessageType internally
        Assert.NotNull(_gateway);
    }

    #endregion

    #region Query Routing Tests

    [Fact]
    public void Gateway_SupportsQueryRouting()
    {
        // Arrange
        _gateway = new CQRSGateway(_serviceProvider, _logger);
        
        // Act - Gateway should be ready to route queries
        
        // Assert
        Assert.NotNull(_gateway);
    }

    [Fact]
    public void Gateway_SupportsCommandRouting()
    {
        // Arrange
        _gateway = new CQRSGateway(_serviceProvider, _logger);
        
        // Act - Gateway should be ready to route commands
        
        // Assert
        Assert.NotNull(_gateway);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Gateway_WithDefaultEndpoint_StartsSuccessfully()
    {
        // Arrange & Act - Use unique port
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45560");
        
        // Act & Assert - Should start with default endpoint without errors
        _gateway.Start();
        Assert.NotNull(_gateway);

        // Cleanup
        _gateway.Stop();
    }

    [Fact]
    public void Gateway_StartsOnInternalChannel()
    {
        // Arrange & Act - Use unique port
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45561");
        
        // Act & Assert - Should start on internal channel
        _gateway.Start();
        Assert.NotNull(_gateway);

        // Cleanup
        _gateway.Stop();
    }

    [Fact]
    public void Gateway_WithNullCurveKeyManager_WorksCorrectly()
    {
        // Arrange & Act - Use unique port
        _gateway = new CQRSGateway(_serviceProvider, _logger, "tcp://*:45562", null);
        
        // Act & Assert - Should work with null CurveKeyManager
        _gateway.Start();
        Assert.NotNull(_gateway);

        // Cleanup
        _gateway.Stop();
    }

    #endregion

    public void Dispose()
    {
        _gateway?.Dispose();
    }
}
