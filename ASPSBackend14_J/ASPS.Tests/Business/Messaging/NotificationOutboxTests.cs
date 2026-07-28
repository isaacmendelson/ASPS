using Business.Messaging;
using Common.Entities;
using Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Business.Data.EF;
using Business.Data.EF.Repositories;
using Microsoft.Data.Sqlite;

namespace ASPS.Tests.Business.Messaging;

/// <summary>
/// ASPS-620: Durable notification delivery with ACK and replay.
/// Tests cover:
///   AC1 — Every notification has a unique, persistent message ID.
///   AC2 — Notifications are persisted (outbox pattern) before ZMQ PUB.
///   AC3 — Devices can ACK notifications by message ID.
///   AC4 — Per-device cursor tracks last ACK'd notification.
///   AC5 — Pending outbox entries are returned for replay on reconnect.
///   AC7 — Idempotent: re-ACKing the same notification does not error.
///   AC8 — Backlog metric (pending count) is exposed.
/// </summary>
public class NotificationOutboxTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationOutboxRepository _repo;

    public NotificationOutboxTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _repo = new NotificationOutboxRepository(_db, loggerFactory.CreateLogger<NotificationOutboxRepository>());
    }

    // ─── AC1 + AC2: Every notification has a unique MessageId and is persisted ───

    [Fact]
    public async Task AddAsync_PersistsNotificationWithUniqueMessageId()
    {
        // Arrange
        var msgId = Guid.NewGuid();
        var entry = MakeEntry(msgId, "device-1");

        // Act
        await _repo.AddAsync(entry);

        // Assert
        var stored = await _db.OutboxNotifications.FindAsync(msgId);
        Assert.NotNull(stored);
        Assert.Equal(msgId, stored.MessageId);
        Assert.Equal("TestNotification", stored.NotificationType);
        Assert.Equal("device-1", stored.DeviceUid);
        Assert.Null(stored.AcknowledgedAt);  // not yet ACK'd
    }

    [Fact]
    public async Task AddAsync_TwoNotifications_HaveDistinctMessageIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        Assert.NotEqual(id1, id2);

        await _repo.AddAsync(MakeEntry(id1, "device-1"));
        await _repo.AddAsync(MakeEntry(id2, "device-1"));

        Assert.Equal(2, _db.OutboxNotifications.Count());
    }

    // ─── AC3: Devices ACK notifications by message ID ───

    [Fact]
    public async Task AcknowledgeAsync_SetsAcknowledgedAt()
    {
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        await _repo.AcknowledgeAsync("device-1", msgId);

        var stored = await _db.OutboxNotifications.FindAsync(msgId);
        Assert.NotNull(stored!.AcknowledgedAt);
    }

    // ─── AC4: Per-device cursor ───

    [Fact]
    public async Task AcknowledgeAsync_UpdatesDeviceCursor()
    {
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        await _repo.AcknowledgeAsync("device-1", msgId);

        var cursor = await _repo.GetCursorAsync("device-1");
        Assert.NotNull(cursor);
        Assert.Equal(msgId, cursor!.LastAcknowledgedMessageId);
        Assert.Equal(1L, cursor.TotalAcknowledged);
    }

    [Fact]
    public async Task AcknowledgeAsync_MultipleAcks_CursorTracksLatestAndIncrementsTotal()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(id1, "device-1"));
        await _repo.AddAsync(MakeEntry(id2, "device-1"));

        await _repo.AcknowledgeAsync("device-1", id1);
        await _repo.AcknowledgeAsync("device-1", id2);

        var cursor = await _repo.GetCursorAsync("device-1");
        Assert.NotNull(cursor);
        Assert.Equal(id2, cursor!.LastAcknowledgedMessageId);
        Assert.Equal(2L, cursor.TotalAcknowledged);
    }

    // ─── AC5: Pending entries for replay ───

    [Fact]
    public async Task GetPendingForDeviceAsync_ReturnsPendingNotAcknowledged()
    {
        var pending1 = Guid.NewGuid();
        var pending2 = Guid.NewGuid();
        var acknowledged = Guid.NewGuid();

        await _repo.AddAsync(MakeEntry(pending1, "device-1"));
        await _repo.AddAsync(MakeEntry(pending2, "device-1"));
        await _repo.AddAsync(MakeEntry(acknowledged, "device-1"));
        await _repo.AcknowledgeAsync("device-1", acknowledged);

        var pending = await _repo.GetPendingForDeviceAsync("device-1");

        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, n => n.MessageId == pending1);
        Assert.Contains(pending, n => n.MessageId == pending2);
        Assert.DoesNotContain(pending, n => n.MessageId == acknowledged);
    }

    [Fact]
    public async Task GetPendingForDeviceAsync_OnlyReturnsThisDevicesNotifications()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _repo.AddAsync(MakeEntry(id1, "device-1"));
        await _repo.AddAsync(MakeEntry(id2, "device-2"));

        var pendingDevice1 = await _repo.GetPendingForDeviceAsync("device-1");
        Assert.Single(pendingDevice1);
        Assert.Equal(id1, pendingDevice1[0].MessageId);
    }

    // ─── AC7: Idempotent — re-ACKing same message does not error ───

    [Fact]
    public async Task AcknowledgeAsync_ReAcking_DoesNotThrow()
    {
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        await _repo.AcknowledgeAsync("device-1", msgId);
        // Second ACK of the same message
        var ex = await Record.ExceptionAsync(() => _repo.AcknowledgeAsync("device-1", msgId));
        Assert.Null(ex);
    }

    [Fact]
    public async Task AcknowledgeAsync_ReAcking_CursorStillIncrements()
    {
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        await _repo.AcknowledgeAsync("device-1", msgId);
        await _repo.AcknowledgeAsync("device-1", msgId);

        var cursor = await _repo.GetCursorAsync("device-1");
        // Two calls = two cursor increments (client re-sent the same ACK)
        Assert.Equal(2L, cursor!.TotalAcknowledged);
    }

    // ─── AC8: Backlog metric ───

    [Fact]
    public async Task GetPendingCountAsync_ReturnsBacklogSize()
    {
        await _repo.AddAsync(MakeEntry(Guid.NewGuid(), "device-1"));
        await _repo.AddAsync(MakeEntry(Guid.NewGuid(), "device-1"));
        var acked = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(acked, "device-1"));
        await _repo.AcknowledgeAsync("device-1", acked);

        var count = await _repo.GetPendingCountAsync();

        Assert.Equal(2, count);
    }

    // ─── Prune ───
    // ExecuteDeleteAsync requires a relational provider — use SQLite for this test.

    [Fact]
    public async Task PruneAcknowledgedAsync_RemovesOldAcknowledgedRows()
    {
        // Arrange: SQLite in-memory (ExecuteDeleteAsync not supported by EF InMemory provider)
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var sqliteDb = new AppDbContext(options);
        await sqliteDb.Database.EnsureCreatedAsync();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var sqliteRepo = new NotificationOutboxRepository(
            sqliteDb,
            loggerFactory.CreateLogger<NotificationOutboxRepository>());

        var msgId = Guid.NewGuid();
        var entry = MakeEntry(msgId, "device-1");
        await sqliteRepo.AddAsync(entry);

        // Force-set AcknowledgedAt to an old timestamp directly
        var stored = await sqliteDb.OutboxNotifications.FindAsync(msgId);
        stored!.AcknowledgedAt = DateTime.UtcNow - TimeSpan.FromDays(10);
        await sqliteDb.SaveChangesAsync();

        // Act
        await sqliteRepo.PruneAcknowledgedAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.Equal(0, await sqliteDb.OutboxNotifications.CountAsync());
    }

    // ─── Major-2 fix: AcknowledgeAsync filters by DeviceUid ───

    [Fact]
    public async Task AcknowledgeAsync_WrongDevice_DoesNotMarkAsAcknowledged()
    {
        // Arrange: notification belongs to device-1
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        // Act: device-2 attempts to ACK device-1's notification
        await _repo.AcknowledgeAsync("device-2", msgId);

        // Assert: notification is still unacknowledged
        var stored = await _db.OutboxNotifications.FindAsync(msgId);
        Assert.Null(stored!.AcknowledgedAt);
    }

    [Fact]
    public async Task AcknowledgeAsync_CorrectDevice_MarksAsAcknowledged()
    {
        // Arrange
        var msgId = Guid.NewGuid();
        await _repo.AddAsync(MakeEntry(msgId, "device-1"));

        // Act: correct device ACKs
        await _repo.AcknowledgeAsync("device-1", msgId);

        // Assert
        var stored = await _db.OutboxNotifications.FindAsync(msgId);
        Assert.NotNull(stored!.AcknowledgedAt);
    }

    // ─── GetCursorAsync: returns null when no ACK ever received ───

    [Fact]
    public async Task GetCursorAsync_ReturnsNull_WhenNoAckReceived()
    {
        var cursor = await _repo.GetCursorAsync("device-never-seen");
        Assert.Null(cursor);
    }

    // ─── Helpers ───

    private static OutboxNotificationEntity MakeEntry(Guid messageId, string deviceUid) =>
        new OutboxNotificationEntity
        {
            MessageId = messageId,
            DeviceUid = deviceUid,
            UserKeyField = "user-1",
            NotificationType = "TestNotification",
            PayloadJson = "{\"type\":\"TestNotification\"}",
            CreatedAt = DateTime.UtcNow,
            DeliveryAttempts = 1
        };

    public void Dispose() => _db.Dispose();
}
