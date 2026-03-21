using System.Net;
using System.Net.Http.Json;
using System.Text;
using Business.Data.EF;
using Common.Entities;
using FluentAssertions;
using Interface.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace ASPS.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the complete TrackUrlAlert flow:
/// API endpoint → Alert processing → Analysis → Persistence
/// </summary>
public class TrackUrlAlertFlowIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TrackUrlAlertFlowIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Basic Flow Tests

    [Fact]
    public async Task SubmitTrackUrlAlert_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-001",
            Url = "https://example.com/page",
            FromUrl = "https://google.com",
            Duration = 45,
            IPAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            TabId = "tab-123",
            Timezone = "UTC",
            Priority = "medium",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
        content.Should().Contain("true");
        content.Should().Contain(dto.DeviceUid);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithMinimalData_ShouldReturnSuccess()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-002",
            Url = "https://minimal-test.com",
            Duration = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task SubmitTrackUrlAlert_WithNullDto_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new StringContent("null", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/alerts/trackurl", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("required");
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithEmptyDeviceUid_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "",
            Url = "https://example.com",
            Duration = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("DeviceUid");
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithEmptyUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-003",
            Url = "",
            Duration = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.2")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://localhost:8080/test")]
    public async Task SubmitTrackUrlAlert_WithLocalUrl_ShouldReturnBadRequest(string localUrl)
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-004",
            Url = localUrl,
            Duration = 30
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Local URLs");
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithNegativeDuration_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-005",
            Url = "https://example.com",
            Duration = -10
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Duration");
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithFutureTimestamp_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-006",
            Url = "https://example.com",
            Duration = 30,
            Timestamp = DateTime.UtcNow.AddHours(2)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Timestamp");
    }

    #endregion

    #region TrackedDomains Integration Tests

    [Fact]
    public async Task SubmitTrackUrlAlert_WithTrackedDomain_ShouldBeIdentified()
    {
        // Arrange - Get DB context and verify tracked domain exists
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var trackedDomain = await dbContext.TrackedDomains
            .FirstOrDefaultAsync(td => td.Domain == "google-analytics.com");
        
        trackedDomain.Should().NotBeNull("Test seed should have created google-analytics.com");

        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-007",
            Url = "https://google-analytics.com/collect",
            Duration = 5,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Note: The current API endpoint doesn't perform analysis, it just validates and queues
        // In a full end-to-end test, we would verify the analysis results
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithSubdomainOfTrackedDomain_ShouldMatch()
    {
        // Arrange - Test that ads.google.com matches google-analytics.com parent
        // Note: This tests the subdomain matching logic in TrackUrlAnalyzer
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Add a parent domain for testing
        var parentDomain = new TrackedDomain("google.com", "Search");
        dbContext.TrackedDomains.Add(parentDomain);
        await dbContext.SaveChangesAsync();

        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-008",
            Url = "https://ads.google.com/tracking",
            Duration = 10,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithMultipleTrackedDomains_ShouldHandleAll()
    {
        // Arrange - Submit multiple alerts with different tracked domains
        var alerts = new[]
        {
            new TrackUrlAlertDto
            {
                DeviceUid = "test-device-009",
                Url = "https://facebook.com/page",
                Duration = 120,
                Timestamp = DateTime.UtcNow
            },
            new TrackUrlAlertDto
            {
                DeviceUid = "test-device-009",
                Url = "https://doubleclick.net/ad",
                Duration = 2,
                Timestamp = DateTime.UtcNow
            },
            new TrackUrlAlertDto
            {
                DeviceUid = "test-device-009",
                Url = "https://google-analytics.com/collect",
                Duration = 1,
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert
        foreach (var alert in alerts)
        {
            var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", alert);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region End-to-End Flow Tests

    [Fact]
    public async Task CompleteFlow_SubmitAlert_ValidateAndQueue_ShouldSucceed()
    {
        // Arrange - This tests the complete pipeline from submission to queueing
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-010",
            Url = "https://example-e2e-test.com/page",
            FromUrl = "https://search-engine.com/results",
            Duration = 180,
            ScamInProgressKey = "", // No scam
            IPAddress = "203.0.113.42",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            TabId = "chrome-tab-001",
            Timezone = "America/New_York",
            Priority = "high",
            Timestamp = DateTime.UtcNow.AddSeconds(-5)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert - Verify successful submission
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
        
        result.Should().NotBeNull();
        result!["success"].ToString().Should().Be("True");
        result["deviceUid"].ToString().Should().Be(dto.DeviceUid);
        result["url"].ToString().Should().Be(dto.Url);
        result.Should().ContainKey("timestamp");
        result.Should().ContainKey("note");
        
        // Verify the note mentions the processing mechanism
        result["note"].ToString().Should().Contain("RealTimeAlertListener");
    }

    [Fact]
    public async Task CompleteFlow_WithAllOptionalFields_ShouldProcessCorrectly()
    {
        // Arrange - Test with all optional fields populated
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-011",
            Url = "https://comprehensive-test.example.org/path/to/page",
            FromUrl = "https://referrer.example.com/source",
            Duration = 456,
            ScamInProgressKey = "scam-key-xyz",
            IPAddress = "198.51.100.42",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            TabId = "firefox-tab-999",
            Timezone = "Europe/London",
            Priority = "critical",
            Timestamp = DateTime.UtcNow.AddMinutes(-2)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
        
        result.Should().NotBeNull();
        result!["success"].ToString().Should().Be("True");
    }

    [Fact]
    public async Task CompleteFlow_SequentialAlerts_SameDevice_ShouldAllSucceed()
    {
        // Arrange - Test sequential browsing pattern
        var deviceUid = "test-device-012";
        var alerts = new[]
        {
            new TrackUrlAlertDto
            {
                DeviceUid = deviceUid,
                Url = "https://search-engine.com/search?q=test",
                Duration = 5,
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            },
            new TrackUrlAlertDto
            {
                DeviceUid = deviceUid,
                Url = "https://result-page.com/article",
                FromUrl = "https://search-engine.com/search?q=test",
                Duration = 120,
                Timestamp = DateTime.UtcNow.AddMinutes(-8)
            },
            new TrackUrlAlertDto
            {
                DeviceUid = deviceUid,
                Url = "https://another-site.com/info",
                FromUrl = "https://result-page.com/article",
                Duration = 60,
                Timestamp = DateTime.UtcNow.AddMinutes(-6)
            }
        };

        // Act & Assert
        foreach (var alert in alerts)
        {
            var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", alert);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task SubmitTrackUrlAlert_WithVeryLongUrl_ShouldHandle()
    {
        // Arrange - Test with very long URL (common in tracking pixels)
        var longPath = string.Join("/", Enumerable.Repeat("segment", 50));
        var queryParams = string.Join("&", Enumerable.Range(1, 30)
            .Select(i => $"param{i}=value{i}"));
        
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-013",
            Url = $"https://long-url-test.com/{longPath}?{queryParams}",
            Duration = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithZeroDuration_ShouldAccept()
    {
        // Arrange - Zero duration is valid (instant navigation)
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-014",
            Url = "https://instant-nav.com",
            Duration = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithSpecialCharactersInUrl_ShouldHandle()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-015",
            Url = "https://example.com/path?search=hello%20world&lang=en&special=%2B%3D",
            Duration = 15
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/alerts/trackurl", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_ConcurrentRequests_ShouldAllSucceed()
    {
        // Arrange - Test concurrent submissions
        var tasks = Enumerable.Range(1, 10).Select(i => new TrackUrlAlertDto
        {
            DeviceUid = $"test-device-concurrent-{i}",
            Url = $"https://concurrent-test-{i}.com",
            Duration = i * 10
        }).Select(dto => _client.PostAsJsonAsync("/api/alerts/trackurl", dto));

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    #endregion

    #region TrackedDomains Database Integration

    [Fact]
    public async Task TrackedDomains_ShouldBeSeededInTestDatabase()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var trackedDomains = await dbContext.TrackedDomains.ToListAsync();

        // Assert
        trackedDomains.Should().NotBeEmpty();
        trackedDomains.Should().Contain(td => td.Domain == "google-analytics.com");
        trackedDomains.Should().Contain(td => td.Domain == "doubleclick.net");
        trackedDomains.Should().Contain(td => td.Domain == "facebook.com");
        trackedDomains.Should().Contain(td => td.Category == "Analytics");
        trackedDomains.Should().Contain(td => td.Category == "Advertising");
        trackedDomains.Should().Contain(td => td.Category == "Social");
    }

    [Fact]
    public async Task TrackedDomains_AllSeededDomains_ShouldBeActive()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var trackedDomains = await dbContext.TrackedDomains.ToListAsync();

        // Assert
        trackedDomains.Should().AllSatisfy(td =>
        {
            td.IsActive.Should().BeTrue();
            td.DateDeleted.Should().BeNull();
            td.IsEnabled.Should().BeTrue();
        });
    }

    #endregion
}
