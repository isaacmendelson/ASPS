using Business.Services;
using Xunit;

namespace ASPS.Tests.Business.Services;

public class RateLimiterTests
{
    [Fact]
    public void IsAllowed_FirstRequest_ShouldReturnTrue()
    {
        // Arrange
        var limiter = new RateLimiter();

        // Act
        var result = limiter.IsAllowed("test-key", 5, TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAllowed_WithinLimit_ShouldReturnTrue()
    {
        // Arrange
        var limiter = new RateLimiter();
        var key = "test-key";
        var maxRequests = 5;
        var window = TimeSpan.FromSeconds(60);

        // Act - make 4 requests (under limit of 5)
        for (int i = 0; i < 4; i++)
        {
            Assert.True(limiter.IsAllowed(key, maxRequests, window));
        }

        // Final request should still be allowed
        var result = limiter.IsAllowed(key, maxRequests, window);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAllowed_ExceedingLimit_ShouldReturnFalse()
    {
        // Arrange
        var limiter = new RateLimiter();
        var key = "test-key";
        var maxRequests = 3;
        var window = TimeSpan.FromSeconds(60);

        // Act - make exactly 3 requests (at limit)
        for (int i = 0; i < 3; i++)
        {
            limiter.IsAllowed(key, maxRequests, window);
        }

        // Try one more request (should exceed limit)
        var result = limiter.IsAllowed(key, maxRequests, window);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAllowed_DifferentKeys_ShouldBeIndependent()
    {
        // Arrange
        var limiter = new RateLimiter();
        var maxRequests = 2;
        var window = TimeSpan.FromSeconds(60);

        // Act - exhaust limit for key1
        limiter.IsAllowed("key1", maxRequests, window);
        limiter.IsAllowed("key1", maxRequests, window);
        var key1Blocked = limiter.IsAllowed("key1", maxRequests, window);

        // key2 should still be allowed
        var key2Allowed = limiter.IsAllowed("key2", maxRequests, window);

        // Assert
        Assert.False(key1Blocked);
        Assert.True(key2Allowed);
    }

    [Fact]
    public void IsAllowed_AfterWindowExpires_ShouldAllowAgain()
    {
        // Arrange
        var limiter = new RateLimiter();
        var key = "test-key";
        var maxRequests = 1;
        var window = TimeSpan.FromMilliseconds(100);

        // Act - make one request (at limit)
        var firstRequest = limiter.IsAllowed(key, maxRequests, window);
        
        // Immediate second request should be blocked
        var secondRequest = limiter.IsAllowed(key, maxRequests, window);

        // Wait for window to expire
        Thread.Sleep(150);

        // Request after window should be allowed again
        var thirdRequest = limiter.IsAllowed(key, maxRequests, window);

        // Assert
        Assert.True(firstRequest);
        Assert.False(secondRequest);
        Assert.True(thirdRequest);
    }

    [Fact]
    public void IsAllowed_SlidingWindow_ShouldWorkCorrectly()
    {
        // Arrange
        var limiter = new RateLimiter();
        var key = "test-key";
        var maxRequests = 2;
        var window = TimeSpan.FromMilliseconds(200);

        // Act
        var req1 = limiter.IsAllowed(key, maxRequests, window); // allowed
        var req2 = limiter.IsAllowed(key, maxRequests, window); // allowed (2/2)
        var req3 = limiter.IsAllowed(key, maxRequests, window); // blocked (3/2)

        Thread.Sleep(250); // Wait for first two requests to expire

        var req4 = limiter.IsAllowed(key, maxRequests, window); // allowed (window reset)

        // Assert
        Assert.True(req1);
        Assert.True(req2);
        Assert.False(req3);
        Assert.True(req4);
    }

    [Fact]
    public void IsAllowed_ZeroMaxRequests_ShouldAlwaysBlock()
    {
        // Arrange
        var limiter = new RateLimiter();

        // Act & Assert
        Assert.False(limiter.IsAllowed("key", 0, TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData("device-1")]
    [InlineData("endpoint:device-2")]
    [InlineData("user:12345")]
    public void IsAllowed_DifferentKeyFormats_ShouldWork(string key)
    {
        // Arrange
        var limiter = new RateLimiter();

        // Act
        var result = limiter.IsAllowed(key, 5, TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAllowed_HighConcurrency_ShouldBeThreadSafe()
    {
        // Arrange
        var limiter = new RateLimiter();
        var key = "test-key";
        var maxRequests = 10;
        var window = TimeSpan.FromSeconds(60);
        var successCount = 0;

        // Act - simulate 20 concurrent requests
        Parallel.For(0, 20, _ =>
        {
            if (limiter.IsAllowed(key, maxRequests, window))
            {
                Interlocked.Increment(ref successCount);
            }
        });

        // Assert - exactly maxRequests should succeed
        Assert.Equal(maxRequests, successCount);
    }

    [Fact]
    public void IsAllowed_MultipleKeys_ShouldCleanupStaleEntries()
    {
        // Arrange
        var limiter = new RateLimiter();
        var window = TimeSpan.FromMilliseconds(50);

        // Act - create requests for multiple keys
        for (int i = 0; i < 10; i++)
        {
            limiter.IsAllowed($"key-{i}", 1, window);
        }

        // Wait for entries to become stale
        Thread.Sleep(100);

        // Trigger cleanup by making new request
        limiter.IsAllowed("cleanup-trigger", 1, window);

        // New requests to old keys should work (entries cleaned up)
        var result = limiter.IsAllowed("key-0", 1, window);

        // Assert
        Assert.True(result);
    }
}
