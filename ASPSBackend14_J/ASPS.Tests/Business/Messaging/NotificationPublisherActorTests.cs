using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Business.Messaging;
using Business.DomainEvents;
using Business.RealtimeAnalysis.UserDomain;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Common.Interfaces;
using Common.Models;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;

namespace ASPS.Tests.Business.Messaging;

public class NotificationPublisherActorTests : IDisposable
{
    private readonly ILogger<NotificationPublisherActor> _logger;
    private NotificationPublisherActor? _actor;
    private readonly List<Mock<NotificationPublisher>> _publisherMocks = new();
    private static int _portCounter = 50200;

    public NotificationPublisherActorTests()
    {
        _logger = NullLogger<NotificationPublisherActor>.Instance;
    }

    private Mock<NotificationPublisher> CreateMockNotificationPublisher()
    {
        var port = System.Threading.Interlocked.Increment(ref _portCounter);
        var endpoint = $"tcp://localhost:{port}";
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ZeroMQ:NotificationPublisher:Endpoint", endpoint }
            })
            .Build();
        
        var mock = new Mock<NotificationPublisher>(
            configuration,
            NullLogger<NotificationPublisher>.Instance,
            endpoint,
            null);
        
        _publisherMocks.Add(mock);
        return mock;
    }

    public void Dispose()
    {
        foreach (var mock in _publisherMocks)
        {
            try
            {
                mock.Object?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesActor()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        
        // Act
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        // Assert
        Assert.NotNull(_actor);
    }

    [Fact]
    public void Constructor_WithNullNotificationPublisher_CreatesActor()
    {
        // Act & Assert
        var actor = new NotificationPublisherActor(null!, _logger);
        Assert.NotNull(actor);
    }

    [Fact]
    public void Constructor_WithNullLogger_CreatesActor()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        
        // Act & Assert
        var actor = new NotificationPublisherActor(mockPublisher.Object, null!);
        Assert.NotNull(actor);
    }

    #endregion

    #region GetHandleableEvents Tests

    [Fact]
    public void GetHandleableEvents_ReturnsAnalysisResultReceived()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        // Act
        var handleableEvents = _actor.GetHandleableEvents();

        // Assert
        handleableEvents.Should().NotBeNull();
        handleableEvents.Should().HaveCount(1);
        handleableEvents.Should().Contain(typeof(AnalysisResultReceived));
    }

    [Fact]
    public void GetHandleableEvents_ReturnsNonEmptyArray()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        // Act
        var handleableEvents = _actor.GetHandleableEvents();

        // Assert
        handleableEvents.Should().NotBeEmpty();
    }

    #endregion

    #region Handle Tests - Valid Events

    [Fact]
    public async Task Handle_WithAnalysisResultReceivedEvent_PublishesNotification()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateValidAnalysisResultReceivedEvent();

        // Act
        await _actor.Handle(analysisEvent);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnalysisResultReceivedEvent_HandlesSuccessfully()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateValidAnalysisResultReceivedEvent();

        // Act & Assert - Should handle without errors
        await _actor.Handle(analysisEvent);
        
        Assert.NotNull(_actor);
    }

    [Fact]
    public async Task Handle_WithValidDeviceUid_PassesCorrectDeviceUid()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var deviceUid = "test-device-123";
        var analysisEvent = CreateValidAnalysisResultReceivedEvent(deviceUid: deviceUid);

        // Act
        await _actor.Handle(analysisEvent);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                deviceUid,
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidUserKey_PassesCorrectUserKey()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var userKey = "user-key-456";
        var analysisEvent = CreateValidAnalysisResultReceivedEvent(userKeyField: userKey);

        // Act
        await _actor.Handle(analysisEvent);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                userKey,
                It.IsAny<AnalysisResultNotification>()),
            Times.Once);
    }

    #endregion

    #region Handle Tests - Edge Cases

    [Fact]
    public async Task Handle_WithEmptyAnalyzerResults_DoesNotPublish()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateAnalysisResultReceivedEventWithEmptyResults();

        // Act
        await _actor.Handle(analysisEvent);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullRiskAssessment_DoesNotPublish()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateAnalysisResultReceivedEventWithNullRiskAssessment();

        // Act
        await _actor.Handle(analysisEvent);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonAnalysisResultReceivedEvent_DoesNothing()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var mockEvent = new Mock<IDomainEvent>();

        // Act
        await _actor.Handle(mockEvent.Object);

        // Assert
        mockPublisher.Verify(
            x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()),
            Times.Never);
    }

    #endregion

    #region Handle Tests - Error Handling

    [Fact]
    public async Task Handle_WhenPublishThrowsException_HandlesError()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        mockPublisher
            .Setup(x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()))
            .Throws(new Exception("Test exception"));

        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateValidAnalysisResultReceivedEvent();

        // Act & Assert - Should handle error without rethrowing
        await _actor.Handle(analysisEvent);
        
        Assert.NotNull(_actor);
    }

    [Fact]
    public async Task Handle_WhenPublishThrowsException_DoesNotRethrow()
    {
        // Arrange
        var mockPublisher = CreateMockNotificationPublisher();
        mockPublisher
            .Setup(x => x.PublishAnalysisResult(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AnalysisResultNotification>()))
            .Throws(new Exception("Test exception"));

        _actor = new NotificationPublisherActor(
            mockPublisher.Object,
            _logger);

        var analysisEvent = CreateValidAnalysisResultReceivedEvent();

        // Act & Assert - Should not throw
        await _actor.Handle(analysisEvent);
    }

    #endregion

    #region Helper Methods

    private AnalysisResultReceived CreateValidAnalysisResultReceivedEvent(
        string deviceUid = "device-123",
        string userKeyField = "user-456")
    {
        var riskAssessment = new RiskAssessment(70f, "High", true, 0.9f); // HIGH risk
        var urlAnalysisResult = new UrlAnalysisResult
        {
            risk_assessment = riskAssessment,
            Url = "http://phishing-site.com"
        };

        var analyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>
        {
            {
                "UrlAnalyzer",
                new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(
                    urlAnalysisResult,
                    Array.Empty<IIndicator>(),
                    Array.Empty<IProtectiveAction>())
            }
        };

        return new AnalysisResultReceived
        {
            DeviceUid = deviceUid,
            UserKeyField = userKeyField,
            AlertType = "PhishingDetected",
            Severity = Severity.High,
            AnalyzerResults = analyzerResults,
            AnalysisTimestamp = DateTime.UtcNow
        };
    }

    private AnalysisResultReceived CreateAnalysisResultReceivedEventWithEmptyResults()
    {
        return new AnalysisResultReceived
        {
            DeviceUid = "device-123",
            UserKeyField = "user-456",
            AlertType = "TestAlert",
            Severity = Severity.Low,
            AnalyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>(),
            AnalysisTimestamp = DateTime.UtcNow
        };
    }

    private AnalysisResultReceived CreateAnalysisResultReceivedEventWithNullRiskAssessment()
    {
        var urlAnalysisResult = new UrlAnalysisResult
        {
            risk_assessment = null!,
            Url = "http://test.com"
        };

        var analyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>
        {
            {
                "UrlAnalyzer",
                new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(
                    urlAnalysisResult,
                    Array.Empty<IIndicator>(),
                    Array.Empty<IProtectiveAction>())
            }
        };

        return new AnalysisResultReceived
        {
            DeviceUid = "device-123",
            UserKeyField = "user-456",
            AlertType = "TestAlert",
            Severity = Severity.Low,
            AnalyzerResults = analyzerResults,
            AnalysisTimestamp = DateTime.UtcNow
        };
    }

    private AnalysisResultReceived CreateAnalysisResultReceivedEventWithTrackUrlAnalysis()
    {
        var riskAssessment = new RiskAssessment(25, "High", true, 0.85f);
        var trackUrlAnalysisResult = new TrackUrlAnalysisResult(
            url: "http://risky-site.com",
            fromUrl: "http://referrer.com",
            duration: 120,
            scamInProgressKey: "scam-key-123",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0",
            tabId: "tab-123",
            timezone: "UTC+2",
            domain: "risky-site.com",
            isSafeDomain: false,
            riskAssessment: riskAssessment);

        var analyzerResults = new Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>>
        {
            {
                "TrackUrlAnalyzer",
                new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(
                    trackUrlAnalysisResult,
                    Array.Empty<IIndicator>(),
                    Array.Empty<IProtectiveAction>())
            }
        };

        return new AnalysisResultReceived
        {
            DeviceUid = "device-track-123",
            UserKeyField = "user-track-456",
            AlertType = "TrackUrlAlert",
            Severity = Severity.High,
            AnalyzerResults = analyzerResults,
            AnalysisTimestamp = DateTime.UtcNow
        };
    }

    #endregion

    #region TrackUrlAnalysisResult Tests

    [Fact]
    public void TrackUrlAnalysisResult_ShouldHaveCorrectProperties()
    {
        // Arrange
        var riskAssessment = new RiskAssessment(30, "Medium", false, 0.75f);
        var trackResult = new TrackUrlAnalysisResult(
            url: "http://test.com",
            fromUrl: "http://prev.com",
            duration: 60,
            scamInProgressKey: "",
            ipAddress: "10.0.0.1",
            userAgent: "TestAgent",
            tabId: "tab-1",
            timezone: "UTC",
            domain: "test.com",
            isSafeDomain: true,
            riskAssessment: riskAssessment);

        // Assert
        trackResult.Url.Should().Be("http://test.com");
        trackResult.FromUrl.Should().Be("http://prev.com");
        trackResult.Duration.Should().Be(60);
        trackResult.TabId.Should().Be("tab-1");
        trackResult.risk_assessment.Should().NotBeNull();
        trackResult.risk_assessment!.risk_score.Should().Be(30);
    }

    [Fact]
    public void Handle_WithTrackUrlAnalysisResult_ShouldExtractRiskAssessment()
    {
        // Arrange
        var analysisEvent = CreateAnalysisResultReceivedEventWithTrackUrlAnalysis();
        
        // Act & Assert - verify the structure contains TrackUrlAnalysisResult
        analysisEvent.AnalyzerResults.Should().ContainKey("TrackUrlAnalyzer");
        var result = analysisEvent.AnalyzerResults["TrackUrlAnalyzer"].Item1;
        result.Should().BeOfType<TrackUrlAnalysisResult>();
        
        var trackResult = result as TrackUrlAnalysisResult;
        trackResult!.risk_assessment.Should().NotBeNull();
        trackResult.risk_assessment!.risk_score.Should().Be(25);
    }

    #endregion
}
