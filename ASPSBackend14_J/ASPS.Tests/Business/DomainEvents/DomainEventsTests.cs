using Business.DomainEvents;
using Business.RealtimeAnalysis;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Moq;
using Xunit;

namespace ASPS.Tests.Business.DomainEvents;

public class DomainEventsTests
{
    [Fact]
    public void DeviceAlertReceived_Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var alert = Mock.Of<DeviceAlert>();
        var priority = Priority.High;
        var deviceUid = "device-123";
        var receiveTime = DateTime.UtcNow;
        var messageTime = DateTime.UtcNow.AddSeconds(-5);
        var entityKey = "alert-key-456";

        // Act
        var @event = new DeviceAlertReceived(alert, priority, deviceUid, receiveTime, messageTime, entityKey);

        // Assert
        Assert.Equal(nameof(DeviceAlertReceived), @event.EventType);
        Assert.Equal(alert, @event.Alert);
        Assert.Equal(priority, @event.Priority);
        Assert.Equal(deviceUid, @event.DeviceUid);
        Assert.Equal(receiveTime, @event.ReceiveTimestamp);
        Assert.Equal(messageTime, @event.MessageTimestamp);
        Assert.Equal(entityKey, @event.DeviceAlertEntityKey);
    }

    [Fact]
    public void DeviceAlertReceived_Timestamp_ShouldBeSet()
    {
        // Arrange
        var alert = Mock.Of<DeviceAlert>();
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var @event = new DeviceAlertReceived(alert, Priority.Low, "device", DateTime.UtcNow, DateTime.UtcNow, "key");
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.InRange(@event.Timestamp, before, after);
    }

    [Fact]
    public void AnalysisResultReceived_DefaultConstructor_ShouldSetEventType()
    {
        // Act
        var @event = new AnalysisResultReceived();

        // Assert
        Assert.Equal(nameof(AnalysisResultReceived), @event.EventType);
        Assert.NotNull(@event.AnalyzerResults);
        Assert.Empty(@event.AnalyzerResults);
        Assert.NotNull(@event.Details);
        Assert.Empty(@event.Details);
    }

    [Fact]
    public void AnalysisResultReceived_Properties_ShouldBeSettable()
    {
        // Arrange
        var @event = new AnalysisResultReceived();
        var userKey = "user-123";
        var deviceAlertKey = "alert-456";
        var deviceUid = "device-789";
        var severity = Severity.Critical;
        var message = "Test message";
        var timestamp = DateTime.UtcNow;

        // Act
        @event.UserKeyField = userKey;
        @event.DeviceAlertKeyField = deviceAlertKey;
        @event.DeviceUid = deviceUid;
        @event.Severity = severity;
        @event.Message = message;
        @event.AnalysisTimestamp = timestamp;

        // Assert
        Assert.Equal(userKey, @event.UserKeyField);
        Assert.Equal(deviceAlertKey, @event.DeviceAlertKeyField);
        Assert.Equal(deviceUid, @event.DeviceUid);
        Assert.Equal(severity, @event.Severity);
        Assert.Equal(message, @event.Message);
        Assert.Equal(timestamp, @event.AnalysisTimestamp);
    }

    [Fact]
    public void SpecificAnalyzerResultReceived_DefaultConstructor_ShouldSetEventType()
    {
        // Act
        var @event = new AnalyzerResultReceived();

        // Assert
        Assert.Equal(nameof(AnalyzerResultReceived), @event.EventType);
        Assert.NotNull(@event.Details);
        Assert.Empty(@event.Details);
    }

    [Fact]
    public void SpecificAnalyzerResultReceived_Properties_ShouldBeSettable()
    {
        // Arrange
        var @event = new AnalyzerResultReceived();
        var analyzerName = "URLAnalyzer";

        // Act
        @event.AnalyzerName = analyzerName;
        @event.Severity = Severity.High;

        // Assert
        Assert.Equal(analyzerName, @event.AnalyzerName);
        Assert.Equal(Severity.High, @event.Severity);
    }

    [Fact]
    public void UserAdded_Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var @event = new UserAdded(user);

        // Assert
        Assert.Equal(nameof(UserAdded), @event.EventType);
        Assert.Equal(user, @event.User);
        Assert.Equal("John", @event.User.FirstName);
    }

    [Fact]
    public void UserUpdated_Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith"
        };

        // Act
        var @event = new UserUpdated(user);

        // Assert
        Assert.Equal(nameof(UserUpdated), @event.EventType);
        Assert.Equal(user, @event.User);
        Assert.Equal("Jane", @event.User.FirstName);
    }

    [Fact]
    public void UserDeleted_Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var userKeyField = "user-key-789";

        // Act
        var @event = new UserDeleted(userKeyField);

        // Assert
        Assert.Equal(nameof(UserDeleted), @event.EventType);
        Assert.Equal(userKeyField, @event.UserKeyField);
    }

    [Fact]
    public void AnalysisResultReceived_AnalyzerResults_CanStoreMultipleResults()
    {
        // Arrange
        var @event = new AnalysisResultReceived();
        var result1 = Mock.Of<AnalysisResult>();
        var result2 = Mock.Of<AnalysisResult>();

        // Act
        @event.AnalyzerResults["Analyzer1"] = new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(
            result1, Array.Empty<IIndicator>(), Array.Empty<IProtectiveAction>());
        @event.AnalyzerResults["Analyzer2"] = new Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>(
            result2, Array.Empty<IIndicator>(), Array.Empty<IProtectiveAction>());

        // Assert
        Assert.Equal(2, @event.AnalyzerResults.Count);
        Assert.True(@event.AnalyzerResults.ContainsKey("Analyzer1"));
        Assert.True(@event.AnalyzerResults.ContainsKey("Analyzer2"));
    }

    [Fact]
    public void AnalysisResultReceived_Details_CanStoreArbitraryData()
    {
        // Arrange
        var @event = new AnalysisResultReceived();

        // Act
        @event.Details["url"] = "http://test.com";
        @event.Details["score"] = 95;
        @event.Details["is_malicious"] = true;

        // Assert
        Assert.Equal(3, @event.Details.Count);
        Assert.Equal("http://test.com", @event.Details["url"]);
        Assert.Equal(95, @event.Details["score"]);
        Assert.Equal(true, @event.Details["is_malicious"]);
    }

    [Theory]
    [InlineData(Priority.Low)]
    [InlineData(Priority.Medium)]
    [InlineData(Priority.High)]
    [InlineData(Priority.Critical)]
    public void DeviceAlertReceived_SupportsDifferentPriorities(Priority priority)
    {
        // Arrange
        var alert = Mock.Of<DeviceAlert>();

        // Act
        var @event = new DeviceAlertReceived(alert, priority, "device", DateTime.UtcNow, DateTime.UtcNow, "key");

        // Assert
        Assert.Equal(priority, @event.Priority);
    }

    [Theory]
    [InlineData(Severity.Low)]
    [InlineData(Severity.Medium)]
    [InlineData(Severity.High)]
    [InlineData(Severity.Critical)]
    public void AnalysisResultReceived_SupportsDifferentSeverities(Severity severity)
    {
        // Arrange
        var @event = new AnalysisResultReceived();

        // Act
        @event.Severity = severity;

        // Assert
        Assert.Equal(severity, @event.Severity);
    }
}
