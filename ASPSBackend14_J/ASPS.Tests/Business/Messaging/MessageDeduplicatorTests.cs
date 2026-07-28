using Business.Messaging;
using FluentAssertions;

namespace ASPS.Tests.Business.Messaging;

public class MessageDeduplicatorTests
{
    [Fact]
    public void TryBegin_SameMessageIdRetry_IsAcceptedOnlyOnce()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var deduplicator = new MessageDeduplicator(
            TimeSpan.FromMinutes(5), 100, () => clock.Now);
        var messageId = Guid.NewGuid();

        deduplicator.TryBegin(messageId).Should().BeTrue();
        deduplicator.TryBegin(messageId).Should().BeFalse();
    }

    [Fact]
    public void TryBegin_AfterRetention_AllowsRetryAgain()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var deduplicator = new MessageDeduplicator(
            TimeSpan.FromMinutes(5), 100, () => clock.Now);
        var messageId = Guid.NewGuid();

        deduplicator.TryBegin(messageId).Should().BeTrue();
        clock.Now += TimeSpan.FromMinutes(6);

        deduplicator.TryBegin(messageId).Should().BeTrue();
    }

    [Fact]
    public void TryBegin_BoundedCapacity_EvictsOldestEntry()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var deduplicator = new MessageDeduplicator(
            TimeSpan.FromHours(1), 2, () => clock.Now);
        var first = Guid.NewGuid();

        deduplicator.TryBegin(first).Should().BeTrue();
        clock.Now += TimeSpan.FromSeconds(1);
        deduplicator.TryBegin(Guid.NewGuid()).Should().BeTrue();
        clock.Now += TimeSpan.FromSeconds(1);
        deduplicator.TryBegin(Guid.NewGuid()).Should().BeTrue();

        deduplicator.Count.Should().BeLessThanOrEqualTo(2);
        deduplicator.TryBegin(first).Should().BeTrue();
    }

    [Fact]
    public void TryBegin_ConcurrentDuplicate_AllowsExactlyOneSideEffectOwner()
    {
        var deduplicator = new MessageDeduplicator(
            TimeSpan.FromMinutes(5), 100);
        var messageId = Guid.NewGuid();

        var claims = Enumerable.Range(0, 64)
            .AsParallel()
            .Select(_ => deduplicator.TryBegin(messageId))
            .ToArray();

        claims.Count(claimed => claimed).Should().Be(1);
    }

    private sealed class TestClock(DateTimeOffset now)
    {
        public DateTimeOffset Now { get; set; } = now;
    }
}
