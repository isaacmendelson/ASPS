using Business.Messaging;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-620 AC5/AC6: On reconnect the backend replays un-ACK'd notifications
/// via the snapshot service.
///
/// Uses a subclass of NotificationPublisher to capture PublishSnapshot calls
/// without requiring a live ZMQ socket per call (the constructor binds the socket
/// once, then PublishSnapshot is overridden to capture calls).
/// </summary>
public class ReconnectSnapshotServiceTests : IDisposable
{
    private readonly CapturingNotificationPublisher _publisher;
    private readonly Mock<INotificationOutboxRepository> _mockOutboxRepo;
    private readonly ReconnectSnapshotService _sut;

    public ReconnectSnapshotServiceTests()
    {
        var randomPort = new Random().Next(59000, 60000);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "NetMQ:NotificationPublisherPort", randomPort.ToString() }
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _publisher = new CapturingNotificationPublisher(config, loggerFactory.CreateLogger<NotificationPublisher>());

        _mockOutboxRepo = new Mock<INotificationOutboxRepository>();

        var scopeFactory = BuildScopeFactory(_mockOutboxRepo.Object);
        _sut = new ReconnectSnapshotService(
            _publisher,
            scopeFactory,
            loggerFactory.CreateLogger<ReconnectSnapshotService>());
    }

    // ─── AC5: Pending notifications are replayed on reconnect ───

    [Fact]
    public async Task SendSnapshotAsync_EmptyOutbox_SendsOnlyHeader()
    {
        _mockOutboxRepo
            .Setup(r => r.GetPendingForDeviceAsync("device-1"))
            .ReturnsAsync(Array.Empty<OutboxNotificationEntity>());

        await _sut.SendSnapshotAsync("device-1");

        // Only the header snapshot frame is published
        Assert.Single(_publisher.CapturedCalls);
    }

    [Fact]
    public async Task SendSnapshotAsync_WithPending_SendsHeaderPlusPendingPayloads()
    {
        var pending = new List<OutboxNotificationEntity>
        {
            new() { MessageId = Guid.NewGuid(), DeviceUid = "device-1",
                    NotificationType = "ImmediateDangerNotification",
                    PayloadJson = "{\"type\":\"ImmediateDangerNotification\"}", CreatedAt = DateTime.UtcNow },
            new() { MessageId = Guid.NewGuid(), DeviceUid = "device-1",
                    NotificationType = "SetTrackedDomainsNotification",
                    PayloadJson = "{\"type\":\"SetTrackedDomainsNotification\"}", CreatedAt = DateTime.UtcNow }
        };

        _mockOutboxRepo
            .Setup(r => r.GetPendingForDeviceAsync("device-1"))
            .ReturnsAsync(pending);

        await _sut.SendSnapshotAsync("device-1");

        // Header + 2 pending = 3 calls total
        Assert.Equal(3, _publisher.CapturedCalls.Count);
    }

    [Fact]
    public async Task SendSnapshotAsync_EmptyDeviceUid_DoesNotPublish()
    {
        await _sut.SendSnapshotAsync(string.Empty);

        Assert.Empty(_publisher.CapturedCalls);
    }

    // ─── AC6: Snapshot header reports pending count ───

    [Fact]
    public async Task SendSnapshotAsync_HeaderContainsPendingCount()
    {
        _mockOutboxRepo
            .Setup(r => r.GetPendingForDeviceAsync("device-1"))
            .ReturnsAsync(new List<OutboxNotificationEntity>
            {
                new() { MessageId = Guid.NewGuid(), DeviceUid = "device-1",
                        NotificationType = "TestType", PayloadJson = "{}", CreatedAt = DateTime.UtcNow }
            });

        await _sut.SendSnapshotAsync("device-1");

        // First call is the header
        var headerJson = _publisher.CapturedCalls[0].Json;
        Assert.Contains("ReconnectSnapshot", headerJson);
        Assert.Contains("\"PendingNotificationCount\":1", headerJson);
    }

    // ─── Outbox exception does not prevent snapshot emission ───

    [Fact]
    public async Task SendSnapshotAsync_OutboxException_StillSendsHeader()
    {
        _mockOutboxRepo
            .Setup(r => r.GetPendingForDeviceAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("DB unavailable"));

        // Should not throw
        await _sut.SendSnapshotAsync("device-1");

        // Empty snapshot header still sent
        Assert.Single(_publisher.CapturedCalls);
    }

    // ─── Replay delivers payloads in original order ───

    [Fact]
    public async Task SendSnapshotAsync_ReplayedPayloads_AreInOrder()
    {
        var payload1 = "{\"type\":\"First\"}";
        var payload2 = "{\"type\":\"Second\"}";

        _mockOutboxRepo
            .Setup(r => r.GetPendingForDeviceAsync("device-1"))
            .ReturnsAsync(new List<OutboxNotificationEntity>
            {
                new() { MessageId = Guid.NewGuid(), DeviceUid = "device-1",
                        NotificationType = "First", PayloadJson = payload1,
                        CreatedAt = DateTime.UtcNow.AddSeconds(-10) },
                new() { MessageId = Guid.NewGuid(), DeviceUid = "device-1",
                        NotificationType = "Second", PayloadJson = payload2,
                        CreatedAt = DateTime.UtcNow }
            });

        await _sut.SendSnapshotAsync("device-1");

        // Index 0 = header, 1 = first payload, 2 = second payload
        Assert.Equal(3, _publisher.CapturedCalls.Count);
        Assert.Equal(payload1, _publisher.CapturedCalls[1].Json);
        Assert.Equal(payload2, _publisher.CapturedCalls[2].Json);
    }

    // ─── Helpers ───

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

    public void Dispose() => _publisher.Dispose();

    // ─── Test double: overrides PublishSnapshot to capture calls ───

    private sealed class CapturingNotificationPublisher : NotificationPublisher
    {
        public List<(string DeviceUid, string Json)> CapturedCalls { get; } = new();

        public CapturingNotificationPublisher(
            IConfiguration configuration,
            ILogger<NotificationPublisher> logger)
            : base(configuration, logger) { }

        public override void PublishSnapshot(string deviceUid, string json)
        {
            CapturedCalls.Add((deviceUid, json));
            // Do NOT call base — we don't want to send over ZMQ in tests
        }
    }
}
