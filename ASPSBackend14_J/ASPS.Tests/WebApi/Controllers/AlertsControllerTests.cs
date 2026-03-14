using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Controllers;
using Interface.Analysis;

namespace ASPS.Tests.WebApi.Controllers;

/// <summary>
/// Unit tests for AlertsController
/// </summary>
public class AlertsControllerTests
{
    private readonly Mock<ILogger<AlertsController>> _loggerMock;
    private readonly AlertsController _controller;

    public AlertsControllerTests()
    {
        _loggerMock = new Mock<ILogger<AlertsController>>();
        _controller = new AlertsController(_loggerMock.Object);
    }

    #region SubmitTrackUrlAlert Tests

    [Fact]
    public async Task SubmitTrackUrlAlert_WithValidDto_ShouldReturnOk()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            FromUrl = "https://google.com",
            Duration = 30,
            IPAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            TabId = "tab-1",
            Timezone = "UTC",
            Priority = "medium",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();
        
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithNullDto_ShouldReturnBadRequest()
    {
        // Arrange
        TrackUrlAlertDto? dto = null;

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
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
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithEmptyUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "",
            Duration = 30
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithLocalUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "http://localhost:8080/test",
            Duration = 30
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        
        var badRequest = result as BadRequestObjectResult;
        var value = badRequest!.Value;
        value.Should().NotBeNull();
    }

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.2")]
    [InlineData("http://localhost")]
    [InlineData("http://0.0.0.0")]
    public async Task SubmitTrackUrlAlert_WithVariousLocalUrls_ShouldReturnBadRequest(string localUrl)
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = localUrl,
            Duration = 30
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithNegativeDuration_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            Duration = -10
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithFutureTimestamp_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            Duration = 30,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithValidData_ShouldLogInformation()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            Duration = 30,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TrackUrlAlert received")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithZeroDuration_ShouldReturnOk()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            Duration = 0,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitTrackUrlAlert_WithAllOptionalFields_ShouldReturnOk()
    {
        // Arrange
        var dto = new TrackUrlAlertDto
        {
            DeviceUid = "test-device-123",
            Url = "https://example.com",
            FromUrl = "https://referrer.com",
            Duration = 45,
            ScamInProgressKey = "scam-key-123",
            IPAddress = "203.0.113.42",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            TabId = "chrome-tab-42",
            Timezone = "America/New_York",
            Priority = "high",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.SubmitTrackUrlAlert(dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    #endregion
}
