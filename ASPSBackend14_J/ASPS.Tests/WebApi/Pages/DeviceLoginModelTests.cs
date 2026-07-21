using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Business.Services;
using WebApi.Pages;
using System.Collections.Generic;

namespace ASPS.Tests.WebApi.Pages;

/// <summary>
/// Unit tests for DeviceLogin Razor Page (FR-031).
///
/// OnGet: validates DeviceUid is present in the query (BindProperty SupportsGet).
/// OnPost: validates that DeviceUid + Email are non-empty before attempting
///   the backend ZMQ call.
///
/// NOTE: paths that reach SendToBackend() are excluded from unit tests — the
/// ZMQ TrySendFrame has a 5-second timeout and requires a running backend
/// listener. Those paths belong in integration tests.
/// </summary>
public class DeviceLoginModelTests
{
    private readonly DeviceLoginModel _sut;
    private readonly Mock<ILogger<DeviceLoginModel>> _mockLogger;

    public DeviceLoginModelTests()
    {
        _mockLogger = new Mock<ILogger<DeviceLoginModel>>();

        // Build in-memory configuration — CURVE disabled so CurveKeyManager does no file I/O
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "NetMQ:AlertListenerEndpoint", "tcp://localhost:50001" },
                { "Security:CurveEnabled", "false" }
            })
            .Build();

        // Use real CurveKeyManager with CURVE disabled (same pattern as CurveKeyManagerTests)
        var curveLogger = new Mock<ILogger<CurveKeyManager>>();
        var curveKeyManager = new CurveKeyManager(config, curveLogger.Object);

        _sut = new DeviceLoginModel(config, _mockLogger.Object, curveKeyManager);

        // Provide a minimal page context (required by PageModel base class)
        _sut.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Constructor / initial state

    [Fact]
    public void Constructor_CreatesInstance_WithExpectedInitialState()
    {
        // Assert
        _sut.Should().NotBeNull();
        _sut.DeviceUid.Should().BeEmpty();
        _sut.Email.Should().BeEmpty();
        _sut.SuccessMessage.Should().BeNull();
        _sut.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region OnGet

    [Fact]
    public void OnGet_WithEmptyDeviceUid_SetsDescriptiveErrorMessage()
    {
        // Arrange — DeviceUid is empty (no query parameter provided)
        _sut.DeviceUid = string.Empty;

        // Act
        _sut.OnGet();

        // Assert
        _sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _sut.ErrorMessage.Should().Contain("No device ID provided");
    }

    [Fact]
    public void OnGet_WithValidDeviceUid_DoesNotSetErrorMessage()
    {
        // Arrange — DeviceUid is populated (normally bound from ?deviceUid= query string)
        _sut.DeviceUid = "TEST-DEVICE-QGMT-001";

        // Act
        _sut.OnGet();

        // Assert
        _sut.ErrorMessage.Should().BeNull();
        _sut.SuccessMessage.Should().BeNull();
    }

    #endregion

    #region OnPost — validation paths (no ZMQ calls made)

    [Fact]
    public void OnPost_WithEmptyDeviceUid_SetsValidationError_AndDoesNotCallBackend()
    {
        // Arrange
        _sut.DeviceUid = string.Empty;
        _sut.Email = "user@example.com";

        // Act
        _sut.OnPost();

        // Assert — returns early before reaching ZMQ
        _sut.ErrorMessage.Should().Be("Device ID and email are required.");
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public void OnPost_WithEmptyEmail_SetsValidationError_AndDoesNotCallBackend()
    {
        // Arrange
        _sut.DeviceUid = "TEST-DEVICE-001";
        _sut.Email = string.Empty;

        // Act
        _sut.OnPost();

        // Assert — returns early before reaching ZMQ
        _sut.ErrorMessage.Should().Be("Device ID and email are required.");
        _sut.SuccessMessage.Should().BeNull();
    }

    [Fact]
    public void OnPost_WithBothEmpty_SetsValidationError()
    {
        // Arrange
        _sut.DeviceUid = string.Empty;
        _sut.Email = string.Empty;

        // Act
        _sut.OnPost();

        // Assert
        _sut.ErrorMessage.Should().Be("Device ID and email are required.");
    }

    [Fact]
    public void OnPost_AfterOnGet_WithNoDeviceUid_AlsoFailsValidation()
    {
        // Arrange — simulate the page being posted without going through OnGet first
        // (DeviceUid was not set, e.g. form was tampered with)
        _sut.DeviceUid = string.Empty;
        _sut.Email = "victim@example.com";

        // Act
        _sut.OnPost();

        // Assert
        _sut.ErrorMessage.Should().NotBeNullOrEmpty();
        _sut.SuccessMessage.Should().BeNull();
    }

    #endregion
}
