using Business.Messaging;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-620: OutboxNotificationPublisher wraps NotificationPublisher and
/// persists every notification to the outbox before sending over ZMQ.
///
/// Uses a real NotificationPublisher (bound to a random test port, as existing
/// NotificationPublisherTests do) because Moq cannot proxy that class without
/// a live ZMQ socket being constructed.
/// </summary>
public class OutboxNotificationPublisherTests : IDisposable
{
    private readonly NotificationPublisher _inner;
    private readonly Mock<INotificationOutboxRepository> _mockOutboxRepo;
    private readonly OutboxNotificationPublisher _sut;

    public OutboxNotificationPublisherTests()
    {
        var config = BuildConfiguration();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        // Real publisher on a test port (no ZMQ encryption needed for unit tests)
        _inner = new NotificationPublisher(config, loggerFactory.CreateLogger<NotificationPublisher>());

        _mockOutboxRepo = new Mock<INotificationOutboxRepository>();
        _mockOutboxRepo
            .Setup(r => r.AddAsync(It.IsAny<OutboxNotificationEntity>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(_mockOutboxRepo.Object);
        _sut = new OutboxNotificationPublisher(
            _inner,
            scopeFactory,
            loggerFactory.CreateLogger<OutboxNotificationPublisher>());
    }

    // ─── Null payloads are not sent to inner (guard clause) ───

    [Fact]
    public void PublishAnalysisResult_Null_IsSkipped()
    {
        // Should not throw
        _sut.PublishAnalysisResult("dev-1", "user-1", null);
    }

    [Fact]
    public void PublishImmediateDangerEvent_Null_IsSkipped()
    {
        _sut.PublishImmediateDangerEvent("dev-1", "user-1", null);
    }

    [Fact]
    public void PublishImmediateDangerEnded_Null_IsSkipped()
    {
        _sut.PublishImmediateDangerEnded("dev-1", "user-1", null);
    }

    [Fact]
    public void PublishSetTrackedDomains_Null_IsSkipped()
    {
        _sut.PublishSetTrackedDomains(Enumerable.Empty<string>(), "user-1", null);
    }

    // ─── GetBacklogSizeAsync delegates to repository ───

    [Fact]
    public async Task GetBacklogSizeAsync_ReturnsPendingCount()
    {
        _mockOutboxRepo.Setup(r => r.GetPendingCountAsync()).ReturnsAsync(7);

        var size = await _sut.GetBacklogSizeAsync();

        Assert.Equal(7, size);
    }

    [Fact]
    public async Task GetBacklogSizeAsync_OnRepositoryException_ReturnsMinusOne()
    {
        _mockOutboxRepo.Setup(r => r.GetPendingCountAsync()).ThrowsAsync(new Exception("DB down"));

        var size = await _sut.GetBacklogSizeAsync();

        Assert.Equal(-1, size);
    }

    // ─── PublishSetBrowserTabsPolicy passes through without throwing ───

    [Fact]
    public void PublishSetBrowserTabsPolicy_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            _sut.PublishSetBrowserTabsPolicy("dev-1", "user-1", "always", null));
        Assert.Null(ex);
    }

    // ─── Helpers ───

    private static IConfiguration BuildConfiguration()
    {
        var randomPort = new Random().Next(58000, 59000);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "NetMQ:NotificationPublisherPort", randomPort.ToString() }
            })
            .Build();
    }

    private static IServiceScopeFactory BuildScopeFactory(INotificationOutboxRepository repo)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repo);
        var provider = services.BuildServiceProvider();

        var mockFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(provider);
        mockFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        return mockFactory.Object;
    }

    public void Dispose() => _inner.Dispose();
}
