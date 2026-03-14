using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Common.Entities;
using Common.Enums;

namespace ASPS.Tests.WebApi.Services;

/// <summary>
/// Tests for JSON serialization/deserialization of abstract classes using TypeNameHandling.Auto.
/// These tests prevent regression of the TypeNameHandling bug where abstract types were not properly deserialized.
/// TypeNameHandling.Auto adds $type metadata when serializing collections of polymorphic types.
/// </summary>
public class JsonSerializationTests
{
    private readonly JsonSerializerSettings _jsonSettings;

    public JsonSerializationTests()
    {
        // Same settings as CQRSGateway uses for responses
        _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
    }

    #region DeviceAlertEntity List Tests (Real-World Scenario)

    [Fact]
    public void Deserialize_ListOfDeviceAlerts_WithUrlAlert_ReturnsCorrectType()
    {
        // Arrange - simulate server response with UrlAlertEntity in a list
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity 
            { 
                Url = "http://test.com",
                DeviceUid = "test-device",
                AlertType = "UrlAlert",
                Priority = Priority.Medium,
                MAC = "00:11:22:33:44:55"
            }
        };
        var json = JsonConvert.SerializeObject(alerts, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Should().BeOfType<UrlAlertEntity>();
        ((UrlAlertEntity)result[0]).Url.Should().Be("http://test.com");
    }

    [Fact]
    public void Deserialize_ListOfDeviceAlerts_WithTrackUrlAlert_ReturnsCorrectType()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new TrackUrlAlertEntity
            {
                Url = "http://tracker-test.com",
                TrackerCount = 5,
                TrackingType = "Analytics",
                DeviceUid = "test-device-2",
                AlertType = "TrackUrlAlert",
                Priority = Priority.High,
                MAC = "AA:BB:CC:DD:EE:FF"
            }
        };
        var json = JsonConvert.SerializeObject(alerts, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Should().BeOfType<TrackUrlAlertEntity>();
        var trackAlert = (TrackUrlAlertEntity)result[0];
        trackAlert.Url.Should().Be("http://tracker-test.com");
        trackAlert.TrackerCount.Should().Be(5);
        trackAlert.TrackingType.Should().Be("Analytics");
    }

    [Fact]
    public void Deserialize_ListOfDeviceAlerts_WithRemoteAccessAlert_ReturnsCorrectType()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new RemoteAccessAlertEntity
            {
                ConnectionUrl = "rdp://192.168.1.100",
                ConnectionStatus = ConnectionStatus.Open,
                ConnectionsCount = 2,
                RunningProcesses = 3,
                DeviceUid = "test-device-3",
                AlertType = "RemoteAccessAlert",
                Priority = Priority.Critical,
                MAC = "11:22:33:44:55:66"
            }
        };
        var json = JsonConvert.SerializeObject(alerts, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Should().BeOfType<RemoteAccessAlertEntity>();
        var remoteAlert = (RemoteAccessAlertEntity)result[0];
        remoteAlert.ConnectionUrl.Should().Be("rdp://192.168.1.100");
        remoteAlert.ConnectionStatus.Should().Be(ConnectionStatus.Open);
        remoteAlert.ConnectionsCount.Should().Be(2);
    }

    [Fact]
    public void Deserialize_ListOfDeviceAlerts_WithMixedTypes_ReturnsCorrectTypes()
    {
        // Arrange - most realistic scenario: server returns mixed alert types
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity 
            { 
                Url = "http://test1.com",
                DeviceUid = "device-1",
                AlertType = "UrlAlert",
                MAC = "00:00:00:00:00:01"
            },
            new TrackUrlAlertEntity 
            { 
                Url = "http://tracker.com",
                TrackerCount = 3,
                DeviceUid = "device-2",
                AlertType = "TrackUrlAlert",
                MAC = "00:00:00:00:00:02"
            },
            new RemoteAccessAlertEntity
            {
                ConnectionUrl = "rdp://test",
                DeviceUid = "device-3",
                AlertType = "RemoteAccessAlert",
                MAC = "00:00:00:00:00:03"
            }
        };
        var json = JsonConvert.SerializeObject(alerts, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().HaveCount(3);
        result![0].Should().BeOfType<UrlAlertEntity>();
        result![1].Should().BeOfType<TrackUrlAlertEntity>();
        result![2].Should().BeOfType<RemoteAccessAlertEntity>();
    }

    #endregion

    #region UserDevice List Tests

    [Fact]
    public void Deserialize_ListOfUserDevices_WithSmartPhone_ReturnsCorrectType()
    {
        // Arrange
        var devices = new List<UserDevice>
        {
            new SmartPhone 
            { 
                DeviceUid = "test-phone-123",
                PhoneNumber = "+1234567890",
                Make = "Apple",
                Model = "iPhone 14",
                DeviceType = DeviceType.MobilePhone,
                OperatingSystem = OperatingSystemType.IOS,
                MAC = "AA:BB:CC:DD:EE:FF"
            }
        };
        var json = JsonConvert.SerializeObject(devices, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<UserDevice>>(json, _jsonSettings);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Should().BeOfType<SmartPhone>();
        var phone = (SmartPhone)result[0];
        phone.DeviceUid.Should().Be("test-phone-123");
        phone.PhoneNumber.Should().Be("+1234567890");
        phone.Make.Should().Be("Apple");
        phone.Model.Should().Be("iPhone 14");
    }

    [Fact]
    public void Deserialize_ListOfUserDevices_WithMixedTypes_ReturnsCorrectTypes()
    {
        // Arrange
        var devices = new List<UserDevice>
        {
            new SmartPhone 
            { 
                DeviceUid = "phone-1",
                PhoneNumber = "+1111111111",
                Make = "Samsung"
            },
            new PersonalComputer
            {
                DeviceUid = "pc-1",
                Make = "Dell",
                Model = "XPS 15"
            }
        };
        var json = JsonConvert.SerializeObject(devices, _jsonSettings);
        
        // Act
        var result = JsonConvert.DeserializeObject<List<UserDevice>>(json, _jsonSettings);
        
        // Assert
        result.Should().HaveCount(2);
        result![0].Should().BeOfType<SmartPhone>();
        result![1].Should().BeOfType<PersonalComputer>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Deserialize_NullValue_ReturnsNull()
    {
        // Arrange
        string json = "null";
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        string json = "[]";
        
        // Act
        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(json, _jsonSettings);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_MixedAlerts_IncludesTypeMetadata()
    {
        // Arrange
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { Url = "http://test.com", DeviceUid = "device-1" }
        };
        
        // Act
        var json = JsonConvert.SerializeObject(alerts, _jsonSettings);
        
        // Assert - verify that $type is included in the JSON
        json.Should().Contain("$type");
        json.Should().Contain("Common.Entities.UrlAlertEntity");
    }

    #endregion
}
